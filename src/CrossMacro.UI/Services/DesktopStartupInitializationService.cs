using System;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Startup;
using CrossMacro.UI.Views.Tabs;

namespace CrossMacro.UI.Services;

internal sealed class DesktopStartupInitializationService
{
    private readonly Func<ISettingsService> _getSettingsService;
    private readonly Func<IThemeService> _getThemeService;
    private readonly Func<LocalizationService> _getLocalizationService;
    private readonly Func<EditorActionDisplayFormatter> _getEditorActionDisplayFormatter;
    private readonly GuiStartupOptions _startupOptions;

    public DesktopStartupInitializationService(
        Func<ISettingsService> getSettingsService,
        Func<IThemeService> getThemeService,
        Func<LocalizationService> getLocalizationService,
        Func<EditorActionDisplayFormatter> getEditorActionDisplayFormatter,
        GuiStartupOptions startupOptions)
    {
        _getSettingsService = getSettingsService ?? throw new ArgumentNullException(nameof(getSettingsService));
        _getThemeService = getThemeService ?? throw new ArgumentNullException(nameof(getThemeService));
        _getLocalizationService = getLocalizationService ?? throw new ArgumentNullException(nameof(getLocalizationService));
        _getEditorActionDisplayFormatter = getEditorActionDisplayFormatter ?? throw new ArgumentNullException(nameof(getEditorActionDisplayFormatter));
        _startupOptions = startupOptions ?? throw new ArgumentNullException(nameof(startupOptions));
    }

    public DesktopStartupPreferences Initialize()
    {
        var settingsService = _getSettingsService();
        settingsService.Load();

        InitializeLocalization(settingsService);
        ApplyTheme(settingsService);

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
    }

    private void ApplyTheme(ISettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(settingsService);

        var themeService = _getThemeService();
        if (!themeService.TryApplyTheme(settingsService.Current.Theme, out var themeError))
        {
            Log.Warning("[App] Theme apply fallback triggered for '{Theme}': {Error}", settingsService.Current.Theme, themeError);
            settingsService.Current.Theme = themeService.CurrentTheme;
            try
            {
                settingsService.Save();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[App] Failed to persist fallback theme '{Theme}'", settingsService.Current.Theme);
            }
        }
    }
}
