namespace CrossMacro.Platform.Linux.Services.Diagnostics;

/// <summary>The Linux session facts and the input diagnostic snapshot the input doctor checks are derived from.</summary>
internal sealed record LinuxInputState(
    string? SessionType,
    bool IsWayland,
    bool IsX11,
    bool IsFlatpak,
    LinuxInputDiagnosticSnapshot Input)
{
    public bool DaemonDiagnosticsEnabled => Input.DaemonDiagnosticsEnabled;

    public LinuxDaemonSocketAccessResult SocketAccess =>
        Input.DaemonSocketAccess ?? LinuxDaemonSocketAccessResult.Missing(IpcProtocol.DefaultSocketPath);

    public bool DaemonSocketExists => SocketAccess.Status is not LinuxDaemonSocketAccessStatus.Missing;

    public bool DefaultSocketExists => DaemonSocketExists;

    public string? ResolvedSocketPath => DaemonSocketExists ? SocketAccess.SocketPath : null;

    public LinuxDaemonGroupMembershipStatus GroupMembershipStatus => SocketAccess.GroupMembershipStatus;

    public LinuxDaemonHandshakeProbeResult Handshake => Input.DaemonHandshake;

    public bool DaemonHandshakeOk => Handshake.Succeeded;

    public bool DirectFallbackAvailable => Input.DirectInput.IsAvailable;

    public bool UInputWritable => Input.DirectInput.CanWriteUInput;

    public bool PrimaryUInputExists => Input.UInputPrimary.Exists;

    public bool AlternateUInputExists => Input.UInputAlternate.Exists;

    public bool CanWritePrimary => Input.UInputPrimary.CanWrite;

    public bool CanWriteAlternate => Input.UInputAlternate.CanWrite;

    public bool GsrVirtualKeyboardDetected => Input.GsrVirtualKeyboardDetected;

    public bool CurrentSessionHasGroup => GroupMembershipStatus is LinuxDaemonGroupMembershipStatus.Member;

    public bool GroupDatabaseContainsUser => GroupMembershipStatus is LinuxDaemonGroupMembershipStatus.Member or LinuxDaemonGroupMembershipStatus.StaleSession;
}
