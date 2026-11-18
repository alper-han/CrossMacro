namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Explains and previews raw script steps whose first token is a known command.</summary>
internal sealed class EditorRawScriptHints(ILocalizationService localizationService)
{
    internal bool TryGetRawScriptHint(string? step, out string hint)
    {
        if (TryGetRawScreenReadingHint(step, out hint))
        {
            return true;
        }

        hint = GetFirstToken(step)?.ToUpperInvariant() switch
        {
            "WINDOW" => localizationService["Editor_RawScriptHint_Window"],
            "CLIPBOARD" => localizationService["Editor_RawScriptHint_Clipboard"],
            "SHELL" => localizationService["Editor_RawScriptHint_Shell"],
            _ => string.Empty,
        };

        return hint.Length > 0;
    }

    internal bool TryGetRawScreenReadingHint(string? step, out string hint)
    {
        hint = GetFirstToken(step)?.ToUpperInvariant() switch
        {
            "PIXELCOLOR" => localizationService["Editor_RawScreenReadingHint_PixelColor"],
            "WAITCOLOR" => localizationService["Editor_RawScreenReadingHint_WaitColor"],
            "PIXELSEARCH" => localizationService["Editor_RawScreenReadingHint_PixelSearch"],
            "IMAGESEARCH" or "IMAGECLICK" or "WAITIMAGE" => localizationService["Editor_RawScreenReadingHint_ImageSearch"],
            _ => string.Empty,
        };

        return hint.Length > 0;
    }

    internal static bool TryExtractRawScreenReadingColor(string? step, out string colorHex)
    {
        colorHex = string.Empty;
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length >= 4 && tokens[0].Equals("waitcolor", StringComparison.OrdinalIgnoreCase))
        {
            colorHex = NormalizePreviewColor(tokens[3]);
            return colorHex.Length > 0;
        }

        if (tokens.Length >= 6 && tokens[0].Equals("pixelsearch", StringComparison.OrdinalIgnoreCase))
        {
            colorHex = NormalizePreviewColor(tokens[5]);
            return colorHex.Length > 0;
        }

        return false;
    }

    internal static string NormalizePreviewColor(string? value)
    {
        var color = (value ?? string.Empty).Trim().ToUpperInvariant();
        return color.Length is 6 && color.All(Uri.IsHexDigit) ? color : string.Empty;
    }

    private static string? GetFirstToken(string? step) =>
        string.IsNullOrWhiteSpace(step)
            ? null
            : step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
}
