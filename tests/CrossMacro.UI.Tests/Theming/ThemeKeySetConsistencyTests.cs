using System.IO;
using FluentAssertions;

namespace CrossMacro.UI.Tests.Theming;

public class ThemeKeySetConsistencyTests
{
    [Fact]
    public void ThemeFiles_ShouldExposeSameResourceKeySet()
    {
        var themeFiles = ThemeTestFileHelper.GetThemeFiles();
        themeFiles.Should().NotBeEmpty();

        var baselineFile = themeFiles[0];
        var baselineKeys = ThemeTestFileHelper.ReadResourceKeys(baselineFile);

        foreach (var themeFile in themeFiles.Skip(1))
        {
            var keys = ThemeTestFileHelper.ReadResourceKeys(themeFile);
            keys.Should().BeEquivalentTo(
                baselineKeys,
                because:
                $"theme '{Path.GetFileName(themeFile)}' must stay structurally aligned with '{Path.GetFileName(baselineFile)}'");
        }
    }
}
