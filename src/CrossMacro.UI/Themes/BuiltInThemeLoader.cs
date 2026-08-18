using System.Collections.ObjectModel;
using System.Reflection;

namespace CrossMacro.UI.Themes;

/// <summary>
/// Loads the shipped themes from the embedded JSON resources under Themes/.
/// Built-in themes share the exact same schema, parser, and validation as external
/// user themes — adding a new built-in theme is dropping a JSON file into the folder.
/// </summary>
internal static class BuiltInThemeLoader
{
    private const string ThemeFolderMarker = ".Themes.";
    private const string JsonExtension = ".json";
    private const string TemplateSuffix = ".template" + JsonExtension;

    public static IReadOnlyList<ThemeDescriptor> LoadAll()
    {
        var assembly = typeof(BuiltInThemeLoader).Assembly;
        var themes = assembly
            .GetManifestResourceNames()
            .Where(IsThemeResource)
            .Select(name => Load(assembly, name))
            .OrderBy(theme => theme.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ReadOnlyCollection<ThemeDescriptor>(themes);
    }

    private static bool IsThemeResource(string resourceName)
    {
        return resourceName.Contains(ThemeFolderMarker, StringComparison.Ordinal)
            && resourceName.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase)
            && !resourceName.EndsWith(TemplateSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static ThemeDescriptor Load(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException($"Embedded theme resource '{resourceName}' is missing.");

        try
        {
            return ThemeDocumentParser.Parse(stream, ThemeSourceKind.BuiltIn, sourcePath: null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Shipped content must never fail at runtime; if it does, fail loudly with context.
            throw new InvalidDataException($"Embedded theme resource '{resourceName}' is invalid: {ex.Message}", ex);
        }
    }
}
