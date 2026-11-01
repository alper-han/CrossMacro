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
        var colors = document.Palette.ParseColors();
        var appearance = ResolveAppearance(document.Appearance, colors[nameof(ThemePalette.BackgroundColor)]);

        return new ThemeDescriptor(document.Name.Trim(), document.Palette, appearance, sourceKind, sourcePath);
    }

    private static ThemeAppearance ResolveAppearance(string? value, Color background)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            if (string.Equals(value, nameof(ThemeAppearance.Light), StringComparison.OrdinalIgnoreCase))
            {
                return ThemeAppearance.Light;
            }
            if (string.Equals(value, nameof(ThemeAppearance.Dark), StringComparison.OrdinalIgnoreCase))
            {
                return ThemeAppearance.Dark;
            }

            throw new InvalidDataException($"Theme appearance '{value}' must be 'Light' or 'Dark'.");
        }

        return RelativeLuminance(background) > 0.5
            ? ThemeAppearance.Light
            : ThemeAppearance.Dark;
    }

    private static double RelativeLuminance(Color color)
    {
        static double Linearize(byte component)
        {
            var value = component / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linearize(color.R)) + (0.7152 * Linearize(color.G)) + (0.0722 * Linearize(color.B));
    }
}
