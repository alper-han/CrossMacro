using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IKWinScreenShotSupportProbe
{
    KWinScreenShotSupportResult ProbeSupport();
}
