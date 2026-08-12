
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal interface IPortalScreenCastRestoreTokenStore
{
    public Task<string?> LoadRestoreTokenAsync(CancellationToken cancellationToken);

    public Task<string?> LoadRestoreDataAsync(CancellationToken cancellationToken);

    public Task SaveRestoreTokenAsync(string restoreToken);

    public Task SaveRestoreDataAsync(string restoreData);

    public Task ClearRestoreTokenAsync();
}
