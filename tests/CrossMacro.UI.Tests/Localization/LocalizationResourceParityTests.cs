namespace CrossMacro.UI.Tests.Localization;

public sealed partial class LocalizationResourceParityTests
{
    private static readonly string LocalizationDirectory = RepositoryFiles.UiPath("Localization");

    public static IEnumerable<object[]> LocalizedResourceFiles()
    {
        return Directory
            .EnumerateFiles(LocalizationDirectory, "Resources.*.resx")
            .Where(path => !Path.GetFileName(path).Equals("Resources.resx", StringComparison.OrdinalIgnoreCase))
            .Select(path => new object[] { Path.GetFileName(path) })
            .OrderBy(row => (string)row[0], StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(LocalizedResourceFiles))]
    public void LocalizedResourceFile_ShouldMatchBaseResourceKeys(string fileName)
    {
        var baseKeys = ReadKeys(Path.Combine(LocalizationDirectory, "Resources.resx"));
        var localizedKeys = ReadKeys(Path.Combine(LocalizationDirectory, fileName));

        _ = localizedKeys.Should().BeEquivalentTo(baseKeys);
        _ = localizedKeys.Should().OnlyHaveUniqueItems();
    }

    private static IReadOnlyList<string> ReadKeys(string path)
    {
        var content = File.ReadAllText(path);
        return ResourceKeyRegex.Matches(content)
            .Select(match => match.Groups["key"].Value)
            .ToArray();
    }

    [GeneratedRegex("<data name=\"(?<key>[^\"]+)\"", RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex ResourceKeyRegex { get; }
}
