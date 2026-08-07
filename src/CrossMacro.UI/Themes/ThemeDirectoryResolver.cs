namespace CrossMacro.UI.Themes;

internal sealed class ThemeDirectoryResolver : IThemeDirectoryResolver
{
    public string GetThemeDirectoryPath()
    {
        return Path.Combine(PathHelper.GetConfigDirectory(), "themes");
    }
}
