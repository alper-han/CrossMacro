using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace CrossMacro.UI.Tests.Theming;

public partial class StyleCompositionTests
{
    [Fact]
    public void AppStyles_ShouldIncludeExpectedStyleModulesInOrder()
    {
        var repoRoot = ThemeTestFileHelper.FindRepositoryRoot();
        var appStylesPath = Path.Combine(repoRoot, "src", "CrossMacro.UI", "Styles", "AppStyles.axaml");
        var content = File.ReadAllText(appStylesPath);

        var includes = StyleIncludeRegex().Matches(content)
            .Select(match => match.Groups[1].Value)
            .ToArray();

        includes.Should().Equal(
            "/Styles/Base/Foundations.axaml",
            "/Styles/Components/Buttons.axaml",
            "/Styles/Components/ListsAndNavigation.axaml",
            "/Styles/Components/Inputs.axaml",
            "/Styles/Components/SelectionControls.axaml",
            "/Styles/Components/ScrollAndPickers.axaml",
            "/Styles/Components/Editor.axaml",
            "/Styles/Components/HotkeyCapture.axaml",
            "/Styles/Components/TemplateOverrides.axaml");
    }

    [GeneratedRegex("<StyleInclude\\s+Source=\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex StyleIncludeRegex();
}
