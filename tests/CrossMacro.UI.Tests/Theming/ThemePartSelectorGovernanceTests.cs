namespace CrossMacro.UI.Tests.Theming;

public sealed partial class ThemePartSelectorGovernanceTests
{
    [Fact]
    public void PartSelectors_ShouldBeCentralizedInTemplateOverrides()
    {
        var stylesRoot = RepositoryFiles.UiPath("Styles");
        var styleFiles = Directory
            .GetFiles(stylesRoot, "*.axaml", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var templateOverridesPath = Path.Combine(stylesRoot, "Components", "TemplateOverrides.axaml");
        var templatePartFiles = styleFiles
            .Where(path => PartSelectorRegex.IsMatch(File.ReadAllText(path)))
            .ToArray();

        _ = templatePartFiles.Should().Contain(templateOverridesPath);
        _ = templatePartFiles.Should().OnlyContain(path => string.Equals(path, templateOverridesPath, StringComparison.Ordinal));
    }

    [GeneratedRegex("Style\\s+Selector=\"[^\"]*PART_[^\"]*\"", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex PartSelectorRegex { get; }
}
