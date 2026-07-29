
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class PortalScreenCastRestoreTokenStore(ISettingsService settingsService) : IPortalScreenCastRestoreTokenStore
{
    private readonly ISettingsService _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

    public Task<string?> LoadRestoreTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = _settingsService.Current.PortalScreenCastRestoreToken;
        return Task.FromResult<string?>(string.IsNullOrWhiteSpace(token) ? null : token);
    }

    public async Task SaveRestoreTokenAsync(string restoreToken)
    {
        if (string.IsNullOrWhiteSpace(restoreToken))
        {
            return;
        }

        if (StringComparer.Ordinal.Equals(_settingsService.Current.PortalScreenCastRestoreToken, restoreToken))
        {
            return;
        }

        _settingsService.Current.PortalScreenCastRestoreToken = restoreToken;
        await _settingsService.SaveAsync().ConfigureAwait(false);
    }

    public async Task ClearRestoreTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(_settingsService.Current.PortalScreenCastRestoreToken))
        {
            return;
        }

        _settingsService.Current.PortalScreenCastRestoreToken = null;
        await _settingsService.SaveAsync().ConfigureAwait(false);
    }
}
