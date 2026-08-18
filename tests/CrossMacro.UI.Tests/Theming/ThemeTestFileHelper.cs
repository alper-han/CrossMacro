
namespace CrossMacro.UI.Tests.Theming;

internal static partial class ThemeTestFileHelper
{
    private static readonly Regex DynamicResourceRegex = DynamicResourceRegexFactory;

    public static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var marker = Path.Combine(current.FullName, "src", "CrossMacro.UI", "CrossMacro.UI.csproj");
            if (File.Exists(marker))
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

    public static IReadOnlyList<string> GetBuiltInThemeFileNames()
    {
        return Directory
            .GetFiles(GetThemeDirectory(), "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null
                && !name.StartsWith('_')
                && !name.EndsWith(".template", StringComparison.OrdinalIgnoreCase))
            .Select(name => name!)
            .ToArray();
    }

    public static IReadOnlyList<ThemeDescriptor> GetBuiltInThemes()
    {
        return ThemeCatalog.Themes;
    }

    public static HashSet<string> ReadGeneratedResourceKeys(ThemeDescriptor theme)
    {
        return ThemeResourceDictionaryFactory.Create(theme)
            .Keys
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    public static string ReadColorValue(ThemeDescriptor theme, string colorKey)
    {
        return theme.Palette.EnumerateColorValues()
                   .FirstOrDefault(entry => string.Equals(entry.Key, colorKey, StringComparison.Ordinal)).Value
               ?? throw new InvalidOperationException($"Color key '{colorKey}' not found in theme '{theme.Name}'.");
    }

    public static HashSet<string> ExtractDynamicResourceKeys(IEnumerable<string> axamlFiles)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in axamlFiles)
        {
            var content = File.ReadAllText(file);
            foreach (Match match in DynamicResourceRegex.Matches(content))
            {
                _ = keys.Add(match.Groups[1].Value);
            }
        }

        return keys;
    }
    [GeneratedRegex(@"\{DynamicResource\s+(?<key>[A-Za-z0-9\._\-]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex DynamicResourceRegexFactory { get; }
}
