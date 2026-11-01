
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignThemeService : IThemeService
{
    public DesignThemeService(string initialTheme)
    {
        _ = ThemeCatalog.TryResolve(initialTheme, out var descriptor);
        CurrentTheme = descriptor.Name;
    }

    public IReadOnlyList<string> AvailableThemes => ThemeCatalog.ThemeNames;

    public string CurrentTheme { get; private set; }

    public ThemePreferenceMode PreferenceMode { get; private set; } = ThemePreferenceMode.System;

    public ThemeAppearance? GetThemeAppearance(string themeName)
    {
        return ThemeCatalog.TryResolve(themeName, out var descriptor) ? descriptor.Appearance : null;
    }
    public bool TryApplySystemTheme(ThemeVariant? variant, out string themeError)
    {
        if (PreferenceMode is not ThemePreferenceMode.System)
        {
            themeError = string.Empty;
            return true;
        }

        return TryApplyTheme(ReferenceEquals(variant, ThemeVariant.Light) ? "Rose Pine Dawn" : "Mocha", out themeError);
    }

    public bool TrySetPreferenceMode(ThemePreferenceMode mode, out string themeError)
    {
        PreferenceMode = mode;
        themeError = string.Empty;
        return true;
    }

    public bool TryApplyTheme(string themeName, out string themeError)
    {
        var isKnownTheme = ThemeCatalog.TryResolve(themeName, out var descriptor);
        CurrentTheme = descriptor.Name;
        themeError = isKnownTheme
            ? string.Empty
            : $"Unknown theme '{themeName}'. Fallback to {ThemeCatalog.DefaultThemeName} applied.";
        return isKnownTheme;
    }

    public bool TryRefreshThemes(out string themeError)
    {
        themeError = string.Empty;
        return true;
    }
}
