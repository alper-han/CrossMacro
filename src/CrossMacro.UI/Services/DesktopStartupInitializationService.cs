
namespace CrossMacro.UI.Services;

internal sealed class DesktopStartupInitializationService(
    Func<ISettingsService> getSettingsService,
    Func<IThemeService> getThemeService,
    Func<LocalizationService> getLocalizationService,
    Func<EditorActionDisplayFormatter> getEditorActionDisplayFormatter,
    IProfileManager profileManager,
    GuiStartupOptions startupOptions)
{
    private readonly Func<ISettingsService> _getSettingsService = getSettingsService ?? throw new ArgumentNullException(nameof(getSettingsService));
    private readonly Func<IThemeService> _getThemeService = getThemeService ?? throw new ArgumentNullException(nameof(getThemeService));
    private readonly Func<LocalizationService> _getLocalizationService = getLocalizationService ?? throw new ArgumentNullException(nameof(getLocalizationService));
    private readonly Func<EditorActionDisplayFormatter> _getEditorActionDisplayFormatter = getEditorActionDisplayFormatter ?? throw new ArgumentNullException(nameof(getEditorActionDisplayFormatter));
    private readonly IProfileManager _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
    private readonly GuiStartupOptions _startupOptions = startupOptions ?? throw new ArgumentNullException(nameof(startupOptions));

    public async Task<DesktopStartupPreferences> InitializeAsync()
    {
        await _profileManager.InitializeAsync().ConfigureAwait(false);

        var settingsService = _getSettingsService();
        _ = await settingsService.LoadAsync().ConfigureAwait(false);

        InitializeLocalization(settingsService);
        await ApplyThemeAsync(settingsService).ConfigureAwait(false);

        return DesktopStartupPreferences.Resolve(settingsService.Current, _startupOptions);
    }

    private void InitializeLocalization(ISettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(settingsService);

        var localizationService = _getLocalizationService();
        LocalizationBindingSource.Instance.Initialize(localizationService);
        localizationService.SetCulture(settingsService.Current.Language);
        ActionTypeConverters.Configure(_getEditorActionDisplayFormatter());
        ScheduleTaskConverters.Configure(localizationService);
        EditorScriptDisplayConverters.Configure(localizationService);
    }

    private async Task ApplyThemeAsync(ISettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(settingsService);

        var themeService = _getThemeService();
        if (!themeService.TryApplyTheme(settingsService.Current.Theme, out var themeError))
        {
            Log.Warning("[App] Theme apply fallback triggered for '{Theme}': {Error}", settingsService.Current.Theme, themeError);
            settingsService.Current.Theme = themeService.CurrentTheme;
            try
            {
                await settingsService.SaveAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Warning(ex, "[App] Failed to persist fallback theme '{Theme}'", settingsService.Current.Theme);
            }
        }
    }
}
