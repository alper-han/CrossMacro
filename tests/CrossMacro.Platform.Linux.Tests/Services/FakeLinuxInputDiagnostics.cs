namespace CrossMacro.Platform.Linux.Tests.Services;

/// <summary>
/// Reports the Linux input state a fake host would expose, so doctor tests describe the machine (files, device
/// access, daemon answers) and observe the checks derived from it.
/// </summary>
internal sealed class FakeLinuxInputDiagnostics(
    bool daemonDiagnosticsEnabled,
    Func<string, bool> fileExists,
    Func<string, bool> canOpenForWrite,
    Func<bool> hasUsableReadableInputDevices,
    Func<string, string?> readAllTextIfExists,
    Func<string, CancellationToken, ValueTask<LinuxDaemonSocketAccessResult>> socketAccess,
    Func<string, TimeSpan, LinuxDaemonHandshakeProbeResult> handshake) : ILinuxInputDiagnostics
{
    private const string UInputPrimaryPath = "/dev/uinput";
    private const string UInputAlternatePath = "/dev/input/uinput";
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(2);

    public async ValueTask<LinuxInputDiagnosticSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        LinuxDaemonSocketAccessResult? access = null;
        var handshakeResult = LinuxDaemonHandshakeProbeResult.Failed(
            IpcProtocol.DefaultSocketPath,
            HandshakeTimeout,
            LinuxDaemonHandshakeStatus.MissingSocket,
            "Daemon socket is missing.");
        if (daemonDiagnosticsEnabled)
        {
            var probed = await socketAccess(IpcProtocol.DefaultSocketPath, cancellationToken);
            access = probed;
            if (probed.Status is not LinuxDaemonSocketAccessStatus.Missing)
            {
                handshakeResult = handshake(probed.SocketPath, HandshakeTimeout);
            }
        }

        var primary = Probe(UInputPrimaryPath);
        var alternate = Probe(UInputAlternatePath);

        return new LinuxInputDiagnosticSnapshot(
            access,
            handshakeResult,
            LinuxDirectInputFallbackResult.FromAccess(primary.CanWrite || alternate.CanWrite, hasUsableReadableInputDevices()),
            primary,
            alternate,
            LinuxGsrCompatibility.ContainsGsrVirtualKeyboard(readAllTextIfExists(LinuxGsrCompatibility.InputDevicesPath)));
    }

    private LinuxUInputNodeAccess Probe(string path)
    {
        var exists = fileExists(path);
        return new LinuxUInputNodeAccess(exists, exists && canOpenForWrite(path));
    }
}
