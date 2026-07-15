using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using Microsoft.Win32.SafeHandles;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal interface IPortalScreenCastSessionClientFactory
{
    Task<IPortalScreenCastSessionClient> ConnectAsync();
}
