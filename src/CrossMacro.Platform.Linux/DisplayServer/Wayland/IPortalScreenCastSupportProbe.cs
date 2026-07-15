using CrossMacro.Platform.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalScreenCastSupportProbe
{
    PortalScreenCastSupportResult ProbeSupport();
}
