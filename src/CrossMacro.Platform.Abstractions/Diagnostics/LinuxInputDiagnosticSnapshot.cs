namespace CrossMacro.Platform.Abstractions.Diagnostics;

/// <summary>
/// What the Linux input stack observed about daemon IPC and direct device access, as reported to diagnostics.
/// </summary>
/// <param name="DaemonSocketAccess">Socket and group access; null when this package ships without a daemon.</param>
/// <param name="DaemonHandshake">The daemon handshake observation; a missing socket when the daemon is not probed.</param>
/// <param name="DirectInput">Direct uinput write and event read access.</param>
/// <param name="UInputPrimary">The primary uinput node.</param>
/// <param name="UInputAlternate">The alternate uinput node.</param>
/// <param name="GsrVirtualKeyboardDetected">Whether the GPU Screen Recorder virtual keyboard is registered.</param>
public readonly record struct LinuxInputDiagnosticSnapshot(
    LinuxDaemonSocketAccessResult? DaemonSocketAccess,
    LinuxDaemonHandshakeProbeResult DaemonHandshake,
    LinuxDirectInputFallbackResult DirectInput,
    LinuxUInputNodeAccess UInputPrimary,
    LinuxUInputNodeAccess UInputAlternate,
    bool GsrVirtualKeyboardDetected)
{
    /// <summary>The group that owns the daemon socket.</summary>
    public const string DaemonGroupName = "crossmacro";

    public bool DaemonDiagnosticsEnabled => DaemonSocketAccess is not null;

    public bool CanUseDaemon => DaemonSocketAccess is { IsAccessible: true } && DaemonHandshake.Succeeded;
}
