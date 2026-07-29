
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IPortalScreenCastRestoreTokenStore
{
    public Task<string?> LoadRestoreTokenAsync(CancellationToken cancellationToken);

    public Task SaveRestoreTokenAsync(string restoreToken);

    public Task ClearRestoreTokenAsync();
}
