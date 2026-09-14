
namespace CrossMacro.Platform.Linux.Services.Capabilities;

internal sealed class LinuxInputCapabilitySnapshotProvider : ILinuxInputCapabilitySnapshotProvider
{
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, bool> _canOpenForWrite;
    private readonly ILinuxInputDeviceAccessProbe _inputDeviceAccessProbe;
    private readonly Func<string, TimeSpan, LinuxInputCapabilityDetector.DaemonHandshakeProbeResult> _daemonHandshakeProbe;
    private readonly bool _daemonEnabled;
    private readonly Func<DateTime> _utcNow;
    private readonly Func<string, TimeSpan, CancellationToken, ValueTask<LinuxInputCapabilityDetector.DaemonHandshakeProbeResult>> _daemonHandshakeProbeAsync;

    public LinuxInputCapabilitySnapshotProvider()
        : this(
            File.Exists,
            LinuxInputProbeUtilities.CanOpenForWrite,
            new LinuxInputDeviceAccessProbe(),
            LinuxInputCapabilityDetector.ProbeDaemonHandshakeWithinBudget,
            LinuxInputProbeUtilities.GetInputEventCandidates,
            daemonEnabled: true,
            daemonHandshakeProbeAsync: LinuxInputCapabilityDetector.ProbeDaemonHandshakeWithinBudgetAsync)
    { /* Empty */ }

    internal LinuxInputCapabilitySnapshotProvider(bool daemonEnabled)
        : this(
            File.Exists,
            LinuxInputProbeUtilities.CanOpenForWrite,
            new LinuxInputDeviceAccessProbe(),
            LinuxInputCapabilityDetector.ProbeDaemonHandshakeWithinBudget,
            LinuxInputProbeUtilities.GetInputEventCandidates,
            daemonEnabled,
            daemonHandshakeProbeAsync: LinuxInputCapabilityDetector.ProbeDaemonHandshakeWithinBudgetAsync)
    { /* Empty */ }

    public LinuxInputCapabilitySnapshotProvider(
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> canOpenForRead,
        Func<string, TimeSpan, LinuxInputCapabilityDetector.DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates)
        : this(
            fileExists,
            canOpenForWrite,
            new LinuxInputDeviceAccessProbe(() => LinuxInputProbeUtilities.HasReadableInputEventAccess(canOpenForRead, getInputEventCandidates)),
            daemonHandshakeProbe,
            getInputEventCandidates,
            daemonEnabled: true)
    { /* Empty */ }

    internal LinuxInputCapabilitySnapshotProvider(
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> canOpenForRead,
        Func<string, TimeSpan, LinuxInputCapabilityDetector.DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates,
        bool daemonEnabled)
        : this(
            fileExists,
            canOpenForWrite,
            new LinuxInputDeviceAccessProbe(() => LinuxInputProbeUtilities.HasReadableInputEventAccess(canOpenForRead, getInputEventCandidates)),
            daemonHandshakeProbe,
            getInputEventCandidates,
            daemonEnabled)
    { /* Empty */ }

    internal LinuxInputCapabilitySnapshotProvider(
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<bool> hasUsableReadableInputDevices,
        Func<string, TimeSpan, LinuxInputCapabilityDetector.DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates)
        : this(
            fileExists,
            canOpenForWrite,
            new LinuxInputDeviceAccessProbe(hasUsableReadableInputDevices),
            daemonHandshakeProbe,
            getInputEventCandidates,
            daemonEnabled: true)
    { /* Empty */ }

    internal LinuxInputCapabilitySnapshotProvider(
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        ILinuxInputDeviceAccessProbe inputDeviceAccessProbe,
        Func<string, TimeSpan, LinuxInputCapabilityDetector.DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates,
        bool daemonEnabled,
        Func<string, TimeSpan, CancellationToken, ValueTask<LinuxInputCapabilityDetector.DaemonHandshakeProbeResult>>? daemonHandshakeProbeAsync = null,
        Func<DateTime>? utcNow = null)
    {
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _canOpenForWrite = canOpenForWrite ?? throw new ArgumentNullException(nameof(canOpenForWrite));
        _inputDeviceAccessProbe = inputDeviceAccessProbe ?? throw new ArgumentNullException(nameof(inputDeviceAccessProbe));
        _daemonHandshakeProbe = daemonHandshakeProbe ?? throw new ArgumentNullException(nameof(daemonHandshakeProbe));
        _daemonEnabled = daemonEnabled;
        _utcNow = utcNow ?? (static () => TimeProvider.System.GetUtcNow().UtcDateTime);
        _daemonHandshakeProbeAsync = daemonHandshakeProbeAsync ?? AdaptSynchronousHandshakeAsync;
        ArgumentNullException.ThrowIfNull(getInputEventCandidates);
    }

    public LinuxInputCapabilitySnapshot CaptureSnapshot(TimeSpan daemonHandshakeBudget)
    {
        var resolvedSocketPath = _daemonEnabled
            ? LinuxInputProbeUtilities.ResolveAvailableSocketPath(_fileExists)
            : null;
        var daemonSocketExists = resolvedSocketPath is not null;

        var daemonProbeResult = daemonSocketExists
            ? ProbeDaemonHandshake(resolvedSocketPath!, daemonHandshakeBudget)
            : LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(LinuxDaemonHandshakeStatus.MissingSocket);

        bool canUseDirectUInput;
        try
        {
            canUseDirectUInput = LinuxInputProbeUtilities.HasUInputWriteAccess(_canOpenForWrite);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            canUseDirectUInput = false;
        }

        bool canReadInputEvents;
        try
        {
            canReadInputEvents = _inputDeviceAccessProbe.HasUsableReadableInputDevices();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            canReadInputEvents = false;
        }

        return new LinuxInputCapabilitySnapshot(
            ResolvedSocketPath: resolvedSocketPath,
            DaemonSocketExists: daemonSocketExists,
            DaemonHandshakeSucceeded: daemonProbeResult.Succeeded,
            DaemonHandshakeTimedOut: daemonProbeResult.TimedOut,
            CanUseDirectUInput: canUseDirectUInput,
            CanReadInputEvents: canReadInputEvents,
            DaemonHandshakeDiagnostic: LinuxDaemonHandshakeDiagnostics.Create(resolvedSocketPath, daemonProbeResult, daemonHandshakeBudget))
        {
            DaemonObservedAtUtc = _daemonEnabled ? _utcNow() : null,
        };
    }

    public async ValueTask<LinuxInputCapabilitySnapshot> CaptureSnapshotAsync(
        TimeSpan daemonHandshakeBudget,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedSocketPath = _daemonEnabled
            ? await LinuxInputProbeUtilities.ResolveAvailableSocketPathAsync(_fileExists, cancellationToken).ConfigureAwait(false)
            : null;
        var daemonSocketExists = resolvedSocketPath is not null;

        var daemonProbeResult = daemonSocketExists
            ? await ProbeDaemonHandshakeAsync(resolvedSocketPath!, daemonHandshakeBudget, cancellationToken).ConfigureAwait(false)
            : LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(LinuxDaemonHandshakeStatus.MissingSocket);

        var canUseDirectUInput = await ProbeAccessAsync(
            token => LinuxInputProbeUtilities.HasUInputWriteAccessAsync(_canOpenForWrite, token), cancellationToken).ConfigureAwait(false);
        var canReadInputEvents = await ProbeAccessAsync(
            _inputDeviceAccessProbe.HasUsableReadableInputDevicesAsync, cancellationToken).ConfigureAwait(false);

        return new LinuxInputCapabilitySnapshot(
            ResolvedSocketPath: resolvedSocketPath,
            DaemonSocketExists: daemonSocketExists,
            DaemonHandshakeSucceeded: daemonProbeResult.Succeeded,
            DaemonHandshakeTimedOut: daemonProbeResult.TimedOut,
            CanUseDirectUInput: canUseDirectUInput,
            CanReadInputEvents: canReadInputEvents,
            DaemonHandshakeDiagnostic: LinuxDaemonHandshakeDiagnostics.Create(resolvedSocketPath, daemonProbeResult, daemonHandshakeBudget))
        {
            DaemonObservedAtUtc = _daemonEnabled ? _utcNow() : null,
        };
    }


    private LinuxInputCapabilityDetector.DaemonHandshakeProbeResult ProbeDaemonHandshake(string socketPath, TimeSpan timeout)
    {
        try
        {
            return _daemonHandshakeProbe(socketPath, timeout);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(ex);
        }
    }
    private async ValueTask<LinuxInputCapabilityDetector.DaemonHandshakeProbeResult> AdaptSynchronousHandshakeAsync(
        string socketPath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        // Compatibility for injected synchronous probes; production uses cancellable socket I/O.
        return await Task.Run(() => ProbeDaemonHandshake(socketPath, timeout), cancellationToken)
            .WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<LinuxInputCapabilityDetector.DaemonHandshakeProbeResult> ProbeDaemonHandshakeAsync(
        string socketPath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            return await _daemonHandshakeProbeAsync(socketPath, timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(ex);
        }
    }

    private static async ValueTask<bool> ProbeAccessAsync(
        Func<CancellationToken, ValueTask<bool>> probe, CancellationToken cancellationToken)
    {
        try
        {
            return await probe(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

}
