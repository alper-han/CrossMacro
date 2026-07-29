
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
        _ = ThemeCatalog.TryResolve(themeName, out var descriptor);
        CurrentTheme = descriptor.Name;
        themeError = string.Empty;
        return true;
    }
}
