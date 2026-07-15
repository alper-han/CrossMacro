
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal interface IPortalScreenCastSessionClient : IDisposable
{
    Task<PortalScreenCastSession> StartAsync(ScreenReadOptions options, string? restoreToken = null);

    void DisposeIfNotOwnedBySession();
}
