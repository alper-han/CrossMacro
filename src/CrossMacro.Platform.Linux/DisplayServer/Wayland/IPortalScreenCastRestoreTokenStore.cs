
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalScreenCastRestoreTokenStore
{
    string? LoadRestoreToken();

    Task SaveRestoreTokenAsync(string restoreToken);

    Task ClearRestoreTokenAsync();
}
