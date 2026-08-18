
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class PortalScreenCastSessionClientFactory : IPortalScreenCastSessionClientFactory
{
    public static PortalScreenCastSessionClientFactory Instance { get; } = new();

    public async Task<IPortalScreenCastSessionClient> ConnectAsync() => await PortalScreenCastClient.ConnectAsync().ConfigureAwait(false);
}
