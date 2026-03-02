using System.IO;
using CrossMacro.UI.Services;
using FluentAssertions;

namespace CrossMacro.UI.Tests.Theming;

public class ThemeCatalogAlignmentTests
{
    [Fact]
    public void ThemeCatalog_ShouldMatchAppResourceKeysAndThemeFiles()
    {
        var repoRoot = ThemeTestFileHelper.FindRepositoryRoot();
        var appResourceFile = Path.Combine(repoRoot, "src", "CrossMacro.UI", "App.axaml");
        var appResourceKeys = ThemeTestFileHelper.ReadResourceKeys(appResourceFile);

        ThemeCatalog.Themes.Should().NotBeEmpty();
        foreach (var theme in ThemeCatalog.Themes)
        {
            appResourceKeys.Should().Contain(theme.ResourceKey);

            var fullThemePath = Path.Combine(repoRoot, "src", "CrossMacro.UI", theme.SourcePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            File.Exists(fullThemePath).Should().BeTrue($"theme source file should exist for {theme.Name}");
        }
    }
}
