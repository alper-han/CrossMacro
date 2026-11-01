namespace CrossMacro.UI.Themes;

public interface IThemeService
{
    public IReadOnlyList<string> AvailableThemes { get; }
    public string CurrentTheme { get; }
    public bool TryApplyTheme(string themeName, out string themeError);
    public bool TryRefreshThemes(out string themeError);
    public ThemePreferenceMode PreferenceMode { get; }
    public bool TryApplySystemTheme(ThemeVariant? variant, out string themeError);
    public ThemeAppearance? GetThemeAppearance(string themeName);
    public bool TrySetPreferenceMode(ThemePreferenceMode mode, out string themeError);
}
