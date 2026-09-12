
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
