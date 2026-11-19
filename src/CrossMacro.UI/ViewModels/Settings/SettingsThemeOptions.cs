namespace CrossMacro.UI.ViewModels.Settings;

/// <summary>Builds the theme picker entries: the "follow system" entry first, then one entry per installed theme.</summary>
internal static class SettingsThemeOptions
{
    internal static ThemePreferenceOption[] Create(IThemeService themeService, ILocalizationService localization) =>
    [
        new(ThemePreferenceMode.System, localization["Settings_ThemeModeSystem"]),
        .. themeService.AvailableThemes.Select(themeName =>
        {
            var appearance = themeService.GetThemeAppearance(themeName);
            var appearanceName = appearance is ThemeAppearance.Light
                ? localization["Settings_ThemeAppearanceLight"]
                : localization["Settings_ThemeAppearanceDark"];
            return new ThemePreferenceOption(ThemePreferenceMode.Manual, themeName, themeName)
            {
                Appearance = appearance,
                DisplayName = string.Format(localization.CurrentCulture,
                    localization["Settings_ThemeOptionDisplayName"], themeName, appearanceName),
            };
        }),
    ];

    /// <summary>The entry that represents the given mode and theme, or null when no installed theme has that name.</summary>
    internal static ThemePreferenceOption? Find(IReadOnlyList<ThemePreferenceOption> options, ThemePreferenceMode mode, string themeName) =>
        mode is ThemePreferenceMode.System
            ? options[0]
            : options.FirstOrDefault(option => string.Equals(option.ThemeName, themeName, StringComparison.OrdinalIgnoreCase));
}
