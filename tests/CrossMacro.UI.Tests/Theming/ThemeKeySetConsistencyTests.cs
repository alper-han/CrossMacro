
namespace CrossMacro.UI.Tests.Theming;

public sealed class ThemeKeySetConsistencyTests
{
    [Fact]
    public void GeneratedThemeDictionaries_ShouldExposeSameResourceKeySet()
    {
        var themes = ThemeTestFileHelper.GetBuiltInThemes();
        _ = themes.Should().NotBeEmpty();

        var baselineTheme = themes[0];
        var baselineKeys = ThemeTestFileHelper.ReadGeneratedResourceKeys(baselineTheme);

        foreach (var theme in themes.Skip(1))
        {
            var keys = ThemeTestFileHelper.ReadGeneratedResourceKeys(theme);
            _ = keys.Should().BeEquivalentTo(
                baselineKeys,
                because:
                $"theme '{theme.Name}' must stay structurally aligned with '{baselineTheme.Name}'");
        }
    }
}
