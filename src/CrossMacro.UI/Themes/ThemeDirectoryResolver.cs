namespace CrossMacro.UI.Themes;

internal sealed class ThemeDirectoryResolver(ApplicationPaths paths) : IThemeDirectoryResolver
{
    public string GetThemeDirectoryPath() => Path.Combine(paths.ConfigDirectory, "themes");
}
