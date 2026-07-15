namespace CrossMacro.Platform.Abstractions.Diagnostics;

public readonly record struct LinuxDaemonDiagnosticSnapshot(
    string SocketPath,
    LinuxDaemonSocketAccessResult SocketAccess,
    LinuxDaemonGroupMembershipStatus GroupMembershipStatus,
    LinuxDirectInputFallbackResult DirectInputFallback,
    LinuxDaemonHandshakeProbeResult Handshake)
{
    public bool CanUseDaemon => SocketAccess.IsAccessible && Handshake.Succeeded;
}
