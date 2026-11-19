
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class PortalScreenCastSessionClientFactory : IPortalScreenCastSessionClientFactory
{
    public static PortalScreenCastSessionClientFactory Instance { get; } = new();

    public async Task<IPortalScreenCastSessionClient> ConnectAsync(CancellationToken cancellationToken) =>
        await PortalScreenCastClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
}
