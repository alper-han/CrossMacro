
namespace CrossMacro.UI.Tests.Theming;

public sealed class ResourceCoverageTests
{
    [Fact]
    public void DynamicResourceUsages_ShouldResolveAgainstKnownResourceSets()
    {
        var repoRoot = ThemeTestFileHelper.FindRepositoryRoot();
        var uiRoot = Path.Combine(repoRoot, "src", "CrossMacro.UI");
        var axamlFiles = Directory
            .GetFiles(uiRoot, "*.axaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        var dynamicKeys = ThemeTestFileHelper.ExtractDynamicResourceKeys(axamlFiles);
        _ = dynamicKeys.Should().NotBeEmpty();

        var classicThemeFile = Path.Combine(uiRoot, "Themes", "Classic.axaml");
        var themeKeys = ThemeTestFileHelper.ReadResourceKeys(classicThemeFile);

        var appResourceFile = Path.Combine(uiRoot, "App.axaml");
        var appKeys = ThemeTestFileHelper.ReadResourceKeys(appResourceFile);
        var styleFiles = Directory
            .GetFiles(Path.Combine(uiRoot, "Styles"), "*.axaml", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var styleKeys = styleFiles
            .SelectMany(ThemeTestFileHelper.ReadResourceKeys)
            .ToHashSet(StringComparer.Ordinal);

        var missingKeys = dynamicKeys
            .Where(key => !themeKeys.Contains(key) && !appKeys.Contains(key) && !styleKeys.Contains(key))
            .Order(StringComparer.Ordinal)
            .ToArray();

        _ = missingKeys.Should().BeEmpty("every DynamicResource key should be declared in App, theme, or style dictionaries");
    }
}
