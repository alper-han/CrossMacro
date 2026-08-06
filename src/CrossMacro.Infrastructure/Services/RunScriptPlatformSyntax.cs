
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Infrastructure ownership boundary for platform-backed screenshot/path syntax.
/// </summary>
internal static class RunScriptPlatformSyntax
{
    public static bool IsScreenshotStep(string? step) => RunScriptSyntax.IsScreenshotStep(step);

    public static bool IsImageSearchMatchModeKeyword(string? token) =>
        ScreenReadOptionGrammar.GetScriptOptionKind(token) is ScreenReadOptionKind.MatchMode;

    public static bool TryParseImageMatchMode(string? token, out EditorImageMatchMode mode)
    {
        mode = token?.Trim().ToUpperInvariant() switch
        {
            "FIRST" => EditorImageMatchMode.FirstThresholdMatch,
            "BEST" => EditorImageMatchMode.BestMatch,
            _ => default,
        };

        return string.Equals(token?.Trim(), "first", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token?.Trim(), "best", StringComparison.OrdinalIgnoreCase);
    }

    public static string ToImageMatchModeToken(EditorImageMatchMode mode) => mode switch
    {
        EditorImageMatchMode.FirstThresholdMatch => "first",
        EditorImageMatchMode.BestMatch => "best",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Image match mode is invalid."),
    };

    public static string? ValidateScreenshotStep(string step) =>
        TryParseScreenshotStep(step, out _, out var error) ? null : error;

    public static bool TryParseScreenshotStep(
        string step,
        out ScreenshotStep screenshotStep,
        out string? error)
    {
        screenshotStep = default;
        error = null;
        var index = 0;
        if (!TryReadWord(step, ref index, out var command)
            || !string.Equals(command, RunScriptSyntax.ScreenshotCommand, StringComparison.OrdinalIgnoreCase))
        {
            error = "Syntax: screenshot [region <x> <y> <width> <height>] (output <path>)? (clipboard)?";
            return false;
        }

        string? outputPath = null;
        var copyToClipboard = false;
        var useRegion = false;
        var regionX = string.Empty;
        var regionY = string.Empty;
        var regionWidth = string.Empty;
        var regionHeight = string.Empty;

        while (TryReadWord(step, ref index, out var token))
        {
            if (string.Equals(token, "region", StringComparison.OrdinalIgnoreCase))
            {
                if (useRegion)
                {
                    error = $"Unknown screenshot token '{token}'.";
                    return false;
                }
                if (!TryReadWord(step, ref index, out regionX)
                    || !TryReadWord(step, ref index, out regionY)
                    || !TryReadWord(step, ref index, out regionWidth)
                    || !TryReadWord(step, ref index, out regionHeight))
                {
                    error = "Syntax: screenshot region <x> <y> <width> <height> (output <path>)? (clipboard)?";
                    return false;
                }
                if (!ValidateRegionToken(regionX, out error)
                    || !ValidateRegionToken(regionY, out error)
                    || !ValidateRegionToken(regionWidth, out error)
                    || !ValidateRegionToken(regionHeight, out error))
                {
                    return false;
                }
                if (!IsPositiveLiteralOrVariable(regionWidth) || !IsPositiveLiteralOrVariable(regionHeight))
                {
                    error = "Invalid screenshot region size. Expected width and height > 0.";
                    return false;
                }
                useRegion = true;
                continue;
            }
            if (string.Equals(token, "output", StringComparison.OrdinalIgnoreCase))
            {
                if (outputPath is not null)
                {
                    error = "Duplicate screenshot output destination.";
                    return false;
                }
                if (!TryReadOutputPath(step, ref index, out outputPath, out error))
                {
                    return false;
                }
                continue;
            }
            if (string.Equals(token, "clipboard", StringComparison.OrdinalIgnoreCase))
            {
                if (copyToClipboard)
                {
                    error = "Duplicate screenshot clipboard destination.";
                    return false;
                }
                copyToClipboard = true;
                continue;
            }
            error = $"Unknown screenshot token '{token}'.";
            return false;
        }

        if (outputPath is null && !copyToClipboard)
        {
            error = "Screenshot requires at least one destination: output <path> or clipboard.";
            return false;
        }
        screenshotStep = new ScreenshotStep(outputPath, copyToClipboard, useRegion,
            regionX, regionY, regionWidth, regionHeight);
        return true;
    }

    private static bool ValidateRegionToken(string token, out string? error)
    {
        error = null;
        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && !(token.StartsWith('$') && EditorActionScriptTokens.IsValidVariableName(token)))
        {
            error = $"Invalid screenshot region value '{token}'. Expected integer or $variable.";
            return false;
        }
        return true;
    }

    private static bool IsPositiveLiteralOrVariable(string token)
    {
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value > 0;
        }
        return token.StartsWith('$') && EditorActionScriptTokens.IsValidVariableName(token);
    }

    private static bool TryReadWord(string value, ref int index, out string token)
    {
        token = string.Empty;
        SkipWhiteSpace(value, ref index);
        if (index >= value.Length)
        {
            return false;
        }
        var start = index;
        while (index < value.Length && !char.IsWhiteSpace(value[index]))
        {
            index++;
        }
        token = value[start..index];
        return true;
    }

    private static bool TryReadOutputPath(string value, ref int index, out string path, out string? error)
    {
        path = string.Empty;
        error = null;
        SkipWhiteSpace(value, ref index);
        if (index >= value.Length)
        {
            error = "Syntax: screenshot output <path>";
            return false;
        }
        if (value[index] is '"' or '\'')
        {
            var quote = value[index++];
            var builder = new System.Text.StringBuilder();
            while (index < value.Length)
            {
                if (value[index] == '\\' && index + 1 < value.Length && (value[index + 1] == quote || value[index + 1] == '\\'))
                {
                    _ = builder.Append(value[index + 1]);
                    index += 2;
                    continue;
                }
                if (value[index] == quote)
                {
                    break;
                }
                _ = builder.Append(value[index++]);
            }
            if (index >= value.Length)
            {
                error = "Unterminated quoted screenshot output path.";
                return false;
            }
            path = builder.ToString();
            index++;
            if (index < value.Length && !char.IsWhiteSpace(value[index]))
            {
                error = "Quoted screenshot output path must be followed by whitespace or end of step.";
                return false;
            }
        }
        else
        {
            var start = index;
            while (index < value.Length && !char.IsWhiteSpace(value[index]))
            {
                index++;
            }
            path = value[start..index];
        }
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Screenshot output path cannot be empty.";
            return false;
        }
        return true;
    }

    private static void SkipWhiteSpace(string value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }
    }
}
