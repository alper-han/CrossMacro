using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using Microsoft.Win32.SafeHandles;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal interface IPortalScreenCastSessionClient : IDisposable
{
    Task<PortalScreenCastSession> StartAsync(ScreenReadOptions options, string? restoreToken = null);

    void DisposeIfNotOwnedBySession();
}
