using System.Text.Json;

namespace CrossMacro.UI.Themes;

/// <summary>
/// Shared parsing and validation for theme JSON documents. Both the embedded built-in
/// catalog and the external file source go through here so the schema can never drift
/// between shipped and user-provided themes.
/// </summary>
internal static class ThemeDocumentParser
{
    public static ThemeDescriptor Parse(Stream json, ThemeSourceKind sourceKind, string? sourcePath)
    {
        ArgumentNullException.ThrowIfNull(json);

        var document = JsonSerializer.Deserialize(json, ThemeJsonContext.Default.ThemeFileDocument)
            ?? throw new InvalidDataException("Theme file is empty.");

        if (document.Palette is null)
        {
            throw new InvalidDataException("Theme file is missing the 'palette' object.");
        }

        if (string.IsNullOrWhiteSpace(document.Name))
        {
            throw new InvalidDataException("Theme file is missing the 'name' value.");
        }

        // Validates every color value; throws InvalidDataException on the first bad entry.
        _ = document.Palette.ParseColors();

        return new ThemeDescriptor(document.Name.Trim(), document.Palette, sourceKind, sourcePath);
    }
}
