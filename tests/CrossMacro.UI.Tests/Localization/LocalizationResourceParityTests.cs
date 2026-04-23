using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CrossMacro.UI.Tests.Localization;

public class LocalizationResourceParityTests
{
    private static readonly string LocalizationDirectory = FindLocalizationDirectory();

    public static IEnumerable<object[]> LocalizedResourceFiles()
    {
        return Directory
            .EnumerateFiles(LocalizationDirectory, "Resources.*.resx")
            .Where(path => !Path.GetFileName(path).Equals("Resources.resx", StringComparison.OrdinalIgnoreCase))
            .Select(path => new object[] { Path.GetFileName(path) })
            .OrderBy(row => (string)row[0]);
    }

    [Theory]
    [MemberData(nameof(LocalizedResourceFiles))]
    public void LocalizedResourceFile_ShouldMatchBaseResourceKeys(string fileName)
    {
        var baseKeys = ReadKeys(Path.Combine(LocalizationDirectory, "Resources.resx"));
        var localizedKeys = ReadKeys(Path.Combine(LocalizationDirectory, fileName));

        localizedKeys.Should().BeEquivalentTo(baseKeys);
        localizedKeys.Should().OnlyHaveUniqueItems();
    }

    private static IReadOnlyList<string> ReadKeys(string path)
    {
        var content = File.ReadAllText(path);
        return Regex.Matches(content, "<data name=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToArray();
    }

    private static string FindLocalizationDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "src", "CrossMacro.UI", "Localization");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/CrossMacro.UI/Localization from test base directory.");
    }
}
