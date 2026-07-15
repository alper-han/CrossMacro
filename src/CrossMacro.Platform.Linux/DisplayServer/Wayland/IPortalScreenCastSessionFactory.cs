
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalScreenCastSessionFactory
{
    public Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenReadOptions options);

    public Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenRect? requestedRegion, ScreenReadOptions options);
}
