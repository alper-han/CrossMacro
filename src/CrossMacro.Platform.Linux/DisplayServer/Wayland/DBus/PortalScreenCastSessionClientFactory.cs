using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using Microsoft.Win32.SafeHandles;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class PortalScreenCastSessionClientFactory : IPortalScreenCastSessionClientFactory
{
    public static PortalScreenCastSessionClientFactory Instance { get; } = new();

    public async Task<IPortalScreenCastSessionClient> ConnectAsync() => await PortalScreenCastClient.ConnectAsync().ConfigureAwait(false);
}
