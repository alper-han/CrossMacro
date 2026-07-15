
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal interface IPortalScreenCastSessionClientFactory
{
    Task<IPortalScreenCastSessionClient> ConnectAsync();
}
