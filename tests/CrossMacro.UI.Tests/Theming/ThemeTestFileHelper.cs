using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CrossMacro.UI.Tests.Theming;

internal static partial class ThemeTestFileHelper
{
    private static readonly Regex ResourceKeyRegex = ResourceKeyRegexFactory();
    private static readonly Regex DynamicResourceRegex = DynamicResourceRegexFactory();

    public static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var marker = Path.Combine(current.FullName, "src", "CrossMacro.UI", "Themes");
            if (Directory.Exists(marker))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root for theme tests.");
    }

    public static string GetThemeDirectory()
    {
        return Path.Combine(FindRepositoryRoot(), "src", "CrossMacro.UI", "Themes");
    }

    public static IReadOnlyList<string> GetThemeFiles()
    {
        return Directory
            .GetFiles(GetThemeDirectory(), "*.axaml", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static HashSet<string> ReadResourceKeys(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return ResourceKeyRegex.Matches(content)
            .Select(match => match.Groups[1].Value)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.Ordinal);
    }

    public static string ReadColorValue(string filePath, string colorKey)
    {
        var content = File.ReadAllText(filePath);
        var colorRegex = new Regex(
            $"<Color\\s+x:Key=\"{Regex.Escape(colorKey)}\">([^<]+)</Color>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var match = colorRegex.Match(content);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Color key '{colorKey}' not found in {Path.GetFileName(filePath)}");
        }

        return match.Groups[1].Value.Trim();
    }

    public static HashSet<string> ExtractDynamicResourceKeys(IEnumerable<string> axamlFiles)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in axamlFiles)
        {
            var content = File.ReadAllText(file);
            foreach (Match match in DynamicResourceRegex.Matches(content))
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        return keys;
    }

    [GeneratedRegex("x:Key=\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex ResourceKeyRegexFactory();

    [GeneratedRegex(@"\{DynamicResource\s+([A-Za-z0-9\._\-]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex DynamicResourceRegexFactory();
}
