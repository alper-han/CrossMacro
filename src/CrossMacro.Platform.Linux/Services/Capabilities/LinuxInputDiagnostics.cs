
namespace CrossMacro.Platform.Linux.Services.Capabilities;

/// <summary>
/// Reports the shared input capability snapshot to diagnostics, adding the socket access/group detail and
/// per-node uinput facts that only explanations need. The daemon is probed fresh on every call (never from
/// the cached observation) so diagnostics describe the daemon as it is now.
/// </summary>
internal sealed class LinuxInputDiagnostics : ILinuxInputDiagnostics
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(2);

    private readonly ILinuxInputCapabilitySnapshotProvider _snapshots;
    private readonly ILinuxDaemonSocketAccessProbe? _socketAccessProbe;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, bool> _canOpenForWrite;
    private readonly Func<bool> _isGsrVirtualKeyboardActive;

    public LinuxInputDiagnostics(
        ILinuxInputCapabilitySnapshotProvider snapshots,
        ILinuxDaemonSocketAccessProbe? socketAccessProbe,
        Func<bool> isGsrVirtualKeyboardActive)
        : this(snapshots, socketAccessProbe, File.Exists, LinuxInputProbeUtilities.CanOpenForWrite, isGsrVirtualKeyboardActive)
    { /* Empty */ }

    internal LinuxInputDiagnostics(
        ILinuxInputCapabilitySnapshotProvider snapshots,
        ILinuxDaemonSocketAccessProbe? socketAccessProbe,
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<bool> isGsrVirtualKeyboardActive)
    {
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _socketAccessProbe = socketAccessProbe;
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _canOpenForWrite = canOpenForWrite ?? throw new ArgumentNullException(nameof(canOpenForWrite));
        _isGsrVirtualKeyboardActive = isGsrVirtualKeyboardActive ?? throw new ArgumentNullException(nameof(isGsrVirtualKeyboardActive));
    }

    public async ValueTask<LinuxInputDiagnosticSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        LinuxDaemonSocketAccessResult? socketAccess = null;
        var handshake = LinuxDaemonHandshakeProbeResult.Failed(
            IpcProtocol.DefaultSocketPath,
            HandshakeTimeout,
            LinuxDaemonHandshakeStatus.MissingSocket,
            "Daemon socket is missing.");
        if (_socketAccessProbe is not null)
        {
            var access = await _socketAccessProbe.ProbeAsync(
                new LinuxDaemonSocketProbeOptions(IpcProtocol.DefaultSocketPath, LinuxInputDiagnosticSnapshot.DaemonGroupName),
                cancellationToken).ConfigureAwait(false);
            socketAccess = access;
            if (access.Status is not LinuxDaemonSocketAccessStatus.Missing)
            {
                handshake = await _snapshots.ProbeDaemonHandshakeAsync(access.SocketPath, HandshakeTimeout, cancellationToken).ConfigureAwait(false);
            }
        }

        var direct = await _snapshots.ProbeDirectInputAsync(cancellationToken).ConfigureAwait(false);

        return new LinuxInputDiagnosticSnapshot(
            socketAccess,
            handshake,
            direct,
            ProbeNode(LinuxSystemPaths.UInputDevicePath),
            ProbeNode(LinuxSystemPaths.UInputAlternatePath),
            _isGsrVirtualKeyboardActive());
    }

    private LinuxUInputNodeAccess ProbeNode(string path)
    {
        var exists = _fileExists(path);
        return new LinuxUInputNodeAccess(exists, exists && _canOpenForWrite(path));
    }
}
