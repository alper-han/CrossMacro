
namespace CrossMacro.UI.Services;

public interface IThemeService
{
    public IReadOnlyList<string> AvailableThemes { get; }
    public string CurrentTheme { get; }
    public bool TryApplyTheme(string themeName, out string themeError);
}
