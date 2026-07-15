using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.X11;

public interface IX11ScreenCaptureSupportProbe
{
    X11ScreenCaptureSupportResult ProbeSupport();
}
