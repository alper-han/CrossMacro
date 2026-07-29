
namespace CrossMacro.UI.Tests.Theming;

public sealed class ThemeCatalogAlignmentTests
{
    [Fact]
    public void ThemeCatalog_ShouldMatchAppResourceKeysAndThemeFiles()
    {
        var repoRoot = ThemeTestFileHelper.FindRepositoryRoot();
        var appResourceFile = Path.Combine(repoRoot, "src", "CrossMacro.UI", "App.axaml");
        var appResourceKeys = ThemeTestFileHelper.ReadResourceKeys(appResourceFile);

        _ = ThemeCatalog.Themes.Should().NotBeEmpty();
        foreach (var theme in ThemeCatalog.Themes)
        {
            _ = appResourceKeys.Should().Contain(theme.ResourceKey);

            var fullThemePath = Path.Combine(repoRoot, "src", "CrossMacro.UI", theme.SourcePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            _ = File.Exists(fullThemePath).Should().BeTrue($"theme source file should exist for {theme.Name}");
        }
    }
}
