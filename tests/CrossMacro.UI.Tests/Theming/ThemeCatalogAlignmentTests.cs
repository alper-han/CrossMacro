
namespace CrossMacro.UI.Tests.Theming;

public sealed class ThemeCatalogAlignmentTests
{
    [Fact]
    public void ThemeCatalog_ShouldExposeUniqueNamesAndBuildValidResourceDictionaries()
    {
        _ = ThemeCatalog.Themes.Should().NotBeEmpty();
        _ = ThemeCatalog.ThemeNames.Should().OnlyHaveUniqueItems();
        _ = ThemeCatalog.DefaultTheme.Name.Should().Be(ThemeCatalog.DefaultThemeName);

        foreach (var theme in ThemeCatalog.Themes)
        {
            var dictionary = ThemeResourceDictionaryFactory.Create(theme);
            _ = dictionary[ThemeCatalog.ThemeMarkerKey].Should().Be(theme.Name);
        }
    }

    [Fact]
    public void ThemeCatalog_ShouldMatchBuiltInThemeFilesOnDisk()
    {
        // Adding a JSON file under Themes/ is the whole workflow for a new built-in theme;
        // the catalog must embed exactly those files, no more, no less.
        var fileNames = ThemeTestFileHelper.GetBuiltInThemeFileNames();

        _ = fileNames.Should().BeEquivalentTo(
            ThemeCatalog.ThemeNames,
            because: "every built-in theme JSON file under Themes/ must be embedded into the catalog and vice versa");
    }

    [Fact]
    public void ThemeCatalog_DefaultTheme_ShouldStayMocha()
    {
        _ = ThemeCatalog.DefaultTheme.Name.Should().Be("Mocha");
        _ = ThemeCatalog.DefaultThemeName.Should().Be("Mocha");
    }
}
