using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace CrossMacro.UI.Tests.Theming;

public partial class ThemePartSelectorGovernanceTests
{
    [Fact]
    public void PartSelectors_ShouldBeCentralizedInTemplateOverrides()
    {
        var repoRoot = ThemeTestFileHelper.FindRepositoryRoot();
        var stylesRoot = Path.Combine(repoRoot, "src", "CrossMacro.UI", "Styles");
        var styleFiles = Directory
            .GetFiles(stylesRoot, "*.axaml", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var templateOverridesPath = Path.Combine(stylesRoot, "Components", "TemplateOverrides.axaml");
        var templatePartFiles = styleFiles
            .Where(path => PartSelectorRegex().IsMatch(File.ReadAllText(path)))
            .ToArray();

        templatePartFiles.Should().Contain(templateOverridesPath);
        templatePartFiles.Should().OnlyContain(path => string.Equals(path, templateOverridesPath, StringComparison.Ordinal));
    }

    [GeneratedRegex("Style\\s+Selector=\"[^\"]*PART_[^\"]*\"", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex PartSelectorRegex();
}
