using CrossMacro.Platform.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalScreenCastSessionFactory
{
    Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenReadOptions options);

    Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenRect? requestedRegion, ScreenReadOptions options);
}
