
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

    [Theory]
    [InlineData("Latte", ThemeAppearance.Light)]
    [InlineData("Solarized", ThemeAppearance.Light)]
    [InlineData("Mocha", ThemeAppearance.Dark)]
    [InlineData("Classic", ThemeAppearance.Dark)]
    [InlineData("Rose Pine Moon", ThemeAppearance.Dark)]
    [InlineData("Tokyo Night", ThemeAppearance.Dark)]
    [InlineData("Tokyo Night Storm", ThemeAppearance.Dark)]
    [InlineData("Tokyo Night Light", ThemeAppearance.Light)]
    [InlineData("Alucard", ThemeAppearance.Light)]
    [InlineData("Ayu Dark", ThemeAppearance.Dark)]
    [InlineData("Ayu Mirage", ThemeAppearance.Dark)]
    [InlineData("Ayu Light", ThemeAppearance.Light)]
    [InlineData("Everforest Dark Soft", ThemeAppearance.Dark)]
    [InlineData("Everforest Light", ThemeAppearance.Light)]
    [InlineData("Material Oceanic", ThemeAppearance.Dark)]
    [InlineData("Material Lighter", ThemeAppearance.Light)]
    public void BuiltInThemes_ShouldExposeCorrectAppearance(string name, ThemeAppearance expected)
    {
        var theme = ThemeCatalog.Themes.Should().ContainSingle(item => item.Name == name).Subject;
        _ = theme.Appearance.Should().Be(expected);
    }

    [Fact]
    public void BuiltInThemes_ShouldContainAdditionalLightPalettes()
    {
        _ = ThemeCatalog.Themes.Where(theme => theme.Appearance is ThemeAppearance.Light)
            .Select(theme => theme.Name)
            .Should().Contain(["Gruvbox Light", "Nord Light", "Rose Pine Dawn", "Tokyo Night Light", "Alucard", "Ayu Light", "Everforest Light", "Material Lighter"]);
    }

    [Fact]
    public void ThemeCatalog_ShouldMatchBuiltInThemeFilesOnDisk()
    {
        // The built-in theme workflow is file-based: adding a JSON file under Themes/
        // must update the catalog, and this test keeps both lists aligned.
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
