
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalScreenCastRestoreTokenStore
{
    public string? LoadRestoreToken();

    public Task SaveRestoreTokenAsync(string restoreToken);

    public Task ClearRestoreTokenAsync();
}
