
namespace CrossMacro.Platform.Linux.Services;

internal sealed class LinuxInputCapabilitySnapshotProvider : ILinuxInputCapabilitySnapshotProvider
{
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, bool> _canOpenForWrite;
    private readonly ILinuxInputDeviceAccessProbe _inputDeviceAccessProbe;
    private readonly Func<string, TimeSpan, LinuxInputCapabilityDetector.DaemonHandshakeProbeResult> _daemonHandshakeProbe;
    private readonly bool _daemonEnabled;

    public LinuxInputCapabilitySnapshotProvider()
        : this(
            File.Exists,
            LinuxInputProbeUtilities.CanOpenForWrite,
            new LinuxInputDeviceAccessProbe(),
            LinuxInputCapabilityDetector.ProbeDaemonHandshakeWithinBudget,
            LinuxInputProbeUtilities.GetInputEventCandidates,
            daemonEnabled: true)
    { /* Empty */ }

    internal LinuxInputCapabilitySnapshotProvider(bool daemonEnabled)
        : this(
            File.Exists,
            LinuxInputProbeUtilities.CanOpenForWrite,
            new LinuxInputDeviceAccessProbe(),
            LinuxInputCapabilityDetector.ProbeDaemonHandshakeWithinBudget,
            LinuxInputProbeUtilities.GetInputEventCandidates,
            daemonEnabled)
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
        bool daemonEnabled)
    {
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _canOpenForWrite = canOpenForWrite ?? throw new ArgumentNullException(nameof(canOpenForWrite));
        _inputDeviceAccessProbe = inputDeviceAccessProbe ?? throw new ArgumentNullException(nameof(inputDeviceAccessProbe));
        _daemonHandshakeProbe = daemonHandshakeProbe ?? throw new ArgumentNullException(nameof(daemonHandshakeProbe));
        _daemonEnabled = daemonEnabled;
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
            DaemonHandshakeDiagnostic: CreateDaemonHandshakeDiagnostic(resolvedSocketPath, daemonProbeResult, daemonHandshakeBudget));
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
            ? await ValueTask.FromResult(_daemonHandshakeProbe(resolvedSocketPath!, daemonHandshakeBudget)).ConfigureAwait(false)
            : LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(LinuxDaemonHandshakeStatus.MissingSocket);

        var canUseDirectUInput = await LinuxInputProbeUtilities.HasUInputWriteAccessAsync(_canOpenForWrite, cancellationToken).ConfigureAwait(false);
        var canReadInputEvents = await _inputDeviceAccessProbe.HasUsableReadableInputDevicesAsync(cancellationToken).ConfigureAwait(false);

        return new LinuxInputCapabilitySnapshot(
            ResolvedSocketPath: resolvedSocketPath,
            DaemonSocketExists: daemonSocketExists,
            DaemonHandshakeSucceeded: daemonProbeResult.Succeeded,
            DaemonHandshakeTimedOut: daemonProbeResult.TimedOut,
            CanUseDirectUInput: canUseDirectUInput,
            CanReadInputEvents: canReadInputEvents,
            DaemonHandshakeDiagnostic: CreateDaemonHandshakeDiagnostic(resolvedSocketPath, daemonProbeResult, daemonHandshakeBudget));
    }


    private static LinuxDaemonHandshakeProbeResult CreateDaemonHandshakeDiagnostic(
        string? socketPath,
        LinuxInputCapabilityDetector.DaemonHandshakeProbeResult probeResult,
        TimeSpan timeout)
    {
        var resolvedSocketPath = socketPath ?? IpcProtocol.DefaultSocketPath;
        return probeResult.Succeeded
            ? LinuxDaemonHandshakeProbeResult.Success(resolvedSocketPath, timeout)
            : LinuxDaemonHandshakeProbeResult.Failed(
                resolvedSocketPath,
                timeout,
                probeResult.Status,
                probeResult.Failure?.Message,
                probeResult.Failure);
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
}
