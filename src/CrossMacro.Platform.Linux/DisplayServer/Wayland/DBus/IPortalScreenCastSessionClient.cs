
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal interface IPortalScreenCastSessionClient : IDisposable
{
    public Task<PortalScreenCastSession> StartAsync(ScreenReadOptions options, string? restoreToken = null);

    public void DisposeIfNotOwnedBySession();
}
