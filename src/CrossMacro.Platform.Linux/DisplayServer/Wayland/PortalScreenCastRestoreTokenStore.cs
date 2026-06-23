using CrossMacro.Core.Services;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class PortalScreenCastRestoreTokenStore : IPortalScreenCastRestoreTokenStore
{
    private readonly ISettingsService _settingsService;

    public PortalScreenCastRestoreTokenStore(ISettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    public string? LoadRestoreToken()
    {
        var token = _settingsService.Current.PortalScreenCastRestoreToken;
        return string.IsNullOrWhiteSpace(token) ? null : token;
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
