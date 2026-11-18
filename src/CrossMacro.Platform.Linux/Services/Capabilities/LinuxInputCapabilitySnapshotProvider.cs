using CrossMacro.Platform.Linux.Native.Evdev;

namespace CrossMacro.Platform.Linux.Services.Capabilities;

/// <summary>
/// Probes daemon connectivity, uinput write access and readable input devices, caches the answers and
/// resolves the input mode through <see cref="LinuxInputModePolicy"/>. All consumers share this one cache,
/// so every backend decision in the process agrees.
/// </summary>
internal sealed class LinuxInputCapabilitySnapshotProvider : ILinuxInputCapabilitySnapshotProvider, IDisposable
{
    private static readonly TimeSpan DaemonProbeTtl = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DaemonHandshakeProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly bool _daemonEnabled;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, bool> _canOpenForWrite;
    private readonly LinuxInputDeviceAccessProbe _inputDeviceAccessProbe;
    private readonly Func<string, TimeSpan, CancellationToken, ValueTask<LinuxDaemonHandshakeProbeResult>> _daemonHandshakeProbe;
    private readonly Func<DateTime> _utcNow;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly LinuxInputModePolicy _modePolicy = new();

    private string? _resolvedSocketPath;
    private bool _daemonAvailabilityFromSocketHistory;
    private DateTime _lastDaemonProbeUtc = DateTime.MinValue;
    private LinuxDaemonHandshakeProbeResult? _daemonHandshake;
    private bool? _canUseDirectUInput;
    private bool? _canReadInputEvents;

    public LinuxInputCapabilitySnapshotProvider(bool daemonEnabled)
        : this(
            daemonEnabled,
            File.Exists,
            LinuxInputProbeUtilities.CanOpenForWrite,
            new LinuxInputDeviceAccessProbe(),
            static (socketPath, timeout, cancellationToken) =>
                new ValueTask<LinuxDaemonHandshakeProbeResult>(
                    LinuxDaemonHandshakeTransport.ProbeAsync(socketPath, timeout, cancellationToken)),
            static () => DateTime.UtcNow)
    { /* Empty */ }

    internal LinuxInputCapabilitySnapshotProvider(
        bool daemonEnabled,
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        LinuxInputDeviceAccessProbe inputDeviceAccessProbe,
        Func<string, TimeSpan, CancellationToken, ValueTask<LinuxDaemonHandshakeProbeResult>> daemonHandshakeProbe,
        Func<DateTime> utcNow)
    {
        _daemonEnabled = daemonEnabled;
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _canOpenForWrite = canOpenForWrite ?? throw new ArgumentNullException(nameof(canOpenForWrite));
        _inputDeviceAccessProbe = inputDeviceAccessProbe ?? throw new ArgumentNullException(nameof(inputDeviceAccessProbe));
        _daemonHandshakeProbe = daemonHandshakeProbe ?? throw new ArgumentNullException(nameof(daemonHandshakeProbe));
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public LinuxInputCapabilitySnapshot GetSnapshot() =>
        GetSnapshotAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask<LinuxInputCapabilitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _utcNow();
            if (_daemonEnabled && IsDaemonObservationStale(now))
            {
                await RefreshDaemonAsync(now, cancellationToken).ConfigureAwait(false);
            }

            await EnsureDirectInputProbedAsync(cancellationToken).ConfigureAwait(false);

            var mode = _modePolicy.Resolve(
                now,
                _daemonHandshake is { Succeeded: true },
                _canUseDirectUInput ?? false,
                _resolvedSocketPath is not null);
            return new LinuxInputCapabilitySnapshot(
                ResolvedSocketPath: _resolvedSocketPath,
                CanUseDirectUInput: _canUseDirectUInput ?? false,
                CanReadInputEvents: _canReadInputEvents ?? false,
                DaemonHandshakeDiagnostic: _daemonHandshake ?? MissingSocket(),
                ResolvedMode: mode)
            {
                DaemonObservedAtUtc = _lastDaemonProbeUtc == DateTime.MinValue ? null : _lastDaemonProbeUtc,
                DaemonAvailabilityFromSocketHistory = _daemonAvailabilityFromSocketHistory,
            };
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public async ValueTask<bool> ProbeDirectInputAccessAsync(CancellationToken cancellationToken = default) =>
        (await ProbeDirectInputAsync(cancellationToken).ConfigureAwait(false)).IsAvailable;

    public async ValueTask<LinuxDirectInputFallbackResult> ProbeDirectInputAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _canUseDirectUInput = null;
            _canReadInputEvents = null;
            _modePolicy.InvalidateModeResolution();
            await EnsureDirectInputProbedAsync(cancellationToken).ConfigureAwait(false);
            return LinuxDirectInputFallbackResult.FromAccess(_canUseDirectUInput is true, _canReadInputEvents is true);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public void InvalidateDirectInputCache()
    {
        _gate.Wait();
        try
        {
            _canUseDirectUInput = null;
            _canReadInputEvents = null;
            _modePolicy.InvalidateModeResolution();
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public void InvalidateCache()
    {
        _gate.Wait();
        try
        {
            _modePolicy.Invalidate();
            _resolvedSocketPath = null;
            _daemonHandshake = null;
            _lastDaemonProbeUtc = DateTime.MinValue;
            _daemonAvailabilityFromSocketHistory = false;
            _canUseDirectUInput = null;
            _canReadInputEvents = null;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private bool IsDaemonObservationStale(DateTime now) =>
        _lastDaemonProbeUtc == DateTime.MinValue || (now - _lastDaemonProbeUtc) > DaemonProbeTtl;

    private async ValueTask RefreshDaemonAsync(DateTime now, CancellationToken cancellationToken)
    {
        var socketPath = LinuxInputProbeUtilities.ResolveAvailableSocketPath(_fileExists);
        _resolvedSocketPath = socketPath;
        _lastDaemonProbeUtc = now;
        _daemonAvailabilityFromSocketHistory = false;

        if (socketPath is null)
        {
            _daemonHandshake = MissingSocket();
            _modePolicy.ObserveDaemonResult(now, succeeded: false);
            return;
        }

        if (_modePolicy.HasSuccessfulDaemonHistory)
        {
            // The daemon serves one long-lived session at a time, so a second handshake probe can time out
            // while the primary IPC client is healthy. Once daemon mode has worked, socket presence is enough.
            _daemonAvailabilityFromSocketHistory = true;
            _daemonHandshake = LinuxDaemonHandshakeProbeResult.Success(socketPath, DaemonHandshakeProbeTimeout);
            _modePolicy.ObserveDaemonResult(now, succeeded: true);
            return;
        }

        var result = await ProbeDaemonHandshakeAsync(socketPath, DaemonHandshakeProbeTimeout, cancellationToken).ConfigureAwait(false);
        _daemonHandshake = result;
        _modePolicy.ObserveDaemonResult(now, result.Succeeded);

        if (result.TimedOut)
        {
            Log.Warning(
                "[LinuxInputCapabilitySnapshotProvider] Daemon handshake probe timed out after {TimeoutMs}ms.",
                DaemonHandshakeProbeTimeout.TotalMilliseconds);
        }
        else if (!result.Succeeded && result.Exception is { } failure)
        {
            Log.Debug(failure, "[LinuxInputCapabilitySnapshotProvider] Daemon handshake probe failed for {SocketPath}: {Status}", socketPath, result.Status);
        }
    }

    public async ValueTask<LinuxDaemonHandshakeProbeResult> ProbeDaemonHandshakeAsync(
        string socketPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _daemonHandshakeProbe(socketPath, timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return LinuxDaemonHandshakeTransport.Failed(socketPath, timeout, ex);
        }
    }

    private async ValueTask EnsureDirectInputProbedAsync(CancellationToken cancellationToken)
    {
        _canUseDirectUInput ??= ProbeUInputWriteAccess();
        _canReadInputEvents ??= await ProbeReadableInputDevicesAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool ProbeUInputWriteAccess()
    {
        try
        {
            return LinuxInputProbeUtilities.HasUInputWriteAccess(_canOpenForWrite);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[LinuxInputCapabilitySnapshotProvider] Failed probing uinput write access");
            return false;
        }
    }

    private async ValueTask<bool> ProbeReadableInputDevicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _inputDeviceAccessProbe.HasUsableReadableInputDevicesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[LinuxInputCapabilitySnapshotProvider] Failed probing readable input events");
            return false;
        }
    }

    private static LinuxDaemonHandshakeProbeResult MissingSocket() =>
        LinuxDaemonHandshakeProbeResult.Failed(
            IpcProtocol.DefaultSocketPath,
            DaemonHandshakeProbeTimeout,
            LinuxDaemonHandshakeStatus.MissingSocket,
            "Daemon socket is missing.");
}
