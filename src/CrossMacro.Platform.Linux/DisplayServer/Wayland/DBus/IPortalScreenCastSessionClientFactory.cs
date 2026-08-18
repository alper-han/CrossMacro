
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal interface IPortalScreenCastSessionClientFactory
{
    public Task<IPortalScreenCastSessionClient> ConnectAsync();
}
