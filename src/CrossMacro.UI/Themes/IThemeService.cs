namespace CrossMacro.UI.Themes;

public interface IThemeService
{
    public IReadOnlyList<string> AvailableThemes { get; }
    public string CurrentTheme { get; }
    public bool TryApplyTheme(string themeName, out string themeError);
    public bool TryRefreshThemes(out string themeError);
}
