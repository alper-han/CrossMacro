
namespace CrossMacro.Infrastructure.Services;

internal static class RunScriptScreenReadingStepParser
{
    public static bool TryValidateStep(string step, out string? error)
    {
        error = null;
        if (!TryParseCommand(step, out var command, out var parts))
        {
            return false;
        }

        return command switch
        {
            RunScriptScreenReadingCommand.PixelColor => TryValidatePixelColorStep(parts, out error),
            RunScriptScreenReadingCommand.WaitColor => TryValidateWaitColorStep(parts, out error),
            RunScriptScreenReadingCommand.PixelSearch => TryValidatePixelSearchStep(parts, out error),
            RunScriptScreenReadingCommand.ImageSearch => TryValidateImageSearchStep(parts, out error),
            RunScriptScreenReadingCommand.ImageClick => TryValidateImageClickStep(parts, out error),
            RunScriptScreenReadingCommand.WaitImage => TryValidateWaitImageStep(parts, out error),
            _ => false,
        };
    }

    public static bool TryParseCommand(
        string step,
        out RunScriptScreenReadingCommand command,
        out string[] parts)
    {
        command = default;
        parts = SplitStep(step);
        if (parts.Length is 0)
        {
            parts = Array.Empty<string>();
            return false;
        }

        if (string.Equals(parts[0], RunScriptSyntax.PixelColorCommand, StringComparison.OrdinalIgnoreCase))
        {
            command = RunScriptScreenReadingCommand.PixelColor;
            return true;
        }

        if (string.Equals(parts[0], RunScriptSyntax.WaitColorCommand, StringComparison.OrdinalIgnoreCase))
        {
            command = RunScriptScreenReadingCommand.WaitColor;
            return true;
        }

        if (string.Equals(parts[0], RunScriptSyntax.PixelSearchCommand, StringComparison.OrdinalIgnoreCase))
        {
            command = RunScriptScreenReadingCommand.PixelSearch;
            return true;
        }

        if (string.Equals(parts[0], RunScriptSyntax.ImageSearchCommand, StringComparison.OrdinalIgnoreCase))
        {
            command = RunScriptScreenReadingCommand.ImageSearch;
            return true;
        }

        if (string.Equals(parts[0], RunScriptSyntax.ImageClickCommand, StringComparison.OrdinalIgnoreCase))
        {
            command = RunScriptScreenReadingCommand.ImageClick;
            return true;
        }

        if (string.Equals(parts[0], RunScriptSyntax.WaitImageCommand, StringComparison.OrdinalIgnoreCase))
        {
            command = RunScriptScreenReadingCommand.WaitImage;
            return true;
        }

        return false;
    }

    public static string[] SplitStep(string step)
    {
        return step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static PixelSearchVariableLayout GetPixelSearchVariableLayout(IReadOnlyList<string> parts)
    {
        if (parts.Count >= 9 && !IsPixelSearchOptionKeyword(parts[6]) && !IsPixelSearchOptionKeyword(parts[8]))
        {
            return new PixelSearchVariableLayout(parts[6], parts[7], parts[8]);
        }

        if (parts.Count >= 8 && !IsPixelSearchOptionKeyword(parts[6]))
        {
            return new PixelSearchVariableLayout(FoundVariableName: null, parts[6], parts[7]);
        }

        return default;
    }

    public static bool IsPixelSearchToleranceKeyword(string value) =>
        RunScriptSyntax.IsPixelSearchToleranceKeyword(value);

    public static bool IsScreenReadTimeoutKeyword(string value) =>
        RunScriptSyntax.IsImageSearchTimeoutKeyword(value);

    public static bool IsPixelSearchOptionKeyword(string value) =>
        IsPixelSearchToleranceKeyword(value) || IsScreenReadTimeoutKeyword(value);

    public static bool IsImageSearchOptionKeyword(string value) =>
        RunScriptSyntax.IsImageSearchSimilarityKeyword(value)
        || RunScriptSyntax.IsImageSearchDownsampleKeyword(value)
        || RunScriptPlatformSyntax.IsImageSearchMatchModeKeyword(value)
        || RunScriptSyntax.IsImageSearchScaleAwareKeyword(value)
        || RunScriptSyntax.IsImageSearchTimeoutKeyword(value);

    public static bool IsImageMatchOptionKeyword(string value) =>
        RunScriptSyntax.IsImageSearchSimilarityKeyword(value)
        || RunScriptSyntax.IsImageSearchDownsampleKeyword(value)
        || RunScriptPlatformSyntax.IsImageSearchMatchModeKeyword(value)
        || RunScriptSyntax.IsImageSearchScaleAwareKeyword(value);

    private static bool TryValidatePixelColorStep(IReadOnlyList<string> parts, out string? error)
    {
        error = null;
        var isRelative = parts.Count > 1 && string.Equals(parts[1], "rel", StringComparison.OrdinalIgnoreCase);
        var coordinateIndex = isRelative ? 2 : 1;
        var optionStartIndex = isRelative ? 4 : 3;
        if (parts.Count < optionStartIndex)
        {
            error = isRelative
                ? "Invalid pixelcolor syntax. Expected: pixelcolor rel <dx> <dy> [var] [timeout <milliseconds>=0+]."
                : "Invalid pixelcolor syntax. Expected: pixelcolor <x> <y> [var] [timeout <milliseconds>=0+] or pixelcolor rel <dx> <dy> [var] [timeout <milliseconds>=0+].";
            return true;
        }

        if (!AreIntegerTokens(parts[coordinateIndex], parts[coordinateIndex + 1]))
        {
            error = isRelative
                ? "Invalid pixelcolor coordinate. Expected integer dx and dy."
                : "Invalid pixelcolor coordinate. Expected integer x and y.";
            return true;
        }

        if (parts.Count > optionStartIndex && !IsScreenReadTimeoutKeyword(parts[optionStartIndex]))
        {
            if (!EditorActionScriptTokens.IsValidVariableName(parts[optionStartIndex]))
            {
                error = $"Invalid variable name '{parts[optionStartIndex]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
                return true;
            }

            optionStartIndex++;
        }

        var hasTimeout = false;
        while (optionStartIndex < parts.Count)
        {
            if (!IsScreenReadTimeoutKeyword(parts[optionStartIndex])
                || hasTimeout
                || optionStartIndex + 1 >= parts.Count
                || !int.TryParse(parts[optionStartIndex + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs)
                || timeoutMs < 0)
            {
                error = "Invalid pixelcolor timeout. Expected timeout <milliseconds>=0+.";
                return true;
            }

            hasTimeout = true;
            optionStartIndex += 2;
        }

        return true;
    }

    private static bool TryValidateWaitColorStep(IReadOnlyList<string> parts, out string? error)
    {
        error = null;
        if (parts.Count is < 4 or > 6)
        {
            error = "Invalid waitcolor syntax. Expected: waitcolor <x> <y> <color> [timeout_ms] [result_var].";
            return true;
        }

        if (!AreIntegerTokens(parts[1], parts[2]))
        {
            error = "Invalid waitcolor coordinate. Expected integer x and y.";
            return true;
        }

        if (!IsValidTargetColorToken(parts[3], out error))
        {
            return true;
        }

        if (parts.Count >= 5
            && (!int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs) || timeoutMs < 0))
        {
            error = "Invalid waitcolor timeout. Expected integer >= 0.";
            return true;
        }

        if (parts.Count is 6 && !EditorActionScriptTokens.IsValidVariableName(parts[5]))
        {
            error = $"Invalid variable name '{parts[5]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
        }

        return true;
    }

    private static bool TryValidatePixelSearchStep(IReadOnlyList<string> parts, out string? error)
    {
        error = null;
        if (parts.Count < 6)
        {
            error = "Invalid pixelsearch syntax. Expected: pixelsearch <x1> <y1> <x2> <y2> <color> [found_var var_x var_y|var_x var_y] [timeout <milliseconds>=0+] [tolerance <0..255>].";
            return true;
        }

        if (!AreIntegerTokens(parts[1], parts[2], parts[3], parts[4]))
        {
            error = "Invalid pixelsearch bounds. Expected integer x1 y1 x2 y2.";
            return true;
        }

        var x1 = int.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture);
        var y1 = int.Parse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture);
        var x2 = int.Parse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture);
        var y2 = int.Parse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture);
        if ((long)Math.Max(x1, x2) - Math.Min(x1, x2) > int.MaxValue
            || (long)Math.Max(y1, y2) - Math.Min(y1, y2) > int.MaxValue)
        {
            error = "Invalid pixelsearch bounds. Endpoint exceeds the supported screen coordinate range.";
            return true;
        }

        if (!IsValidTargetColorToken(parts[5], out error))
        {
            return true;
        }

        var index = 6;
        var variableCount = 0;
        while (index < parts.Count && !IsPixelSearchOptionKeyword(parts[index]))
        {
            variableCount++;
            if (variableCount > 3)
            {
                error = "Invalid pixelsearch syntax. Expected either no variables, x_var y_var, or found_var x_var y_var before options.";
                return true;
            }

            if (!EditorActionScriptTokens.IsValidVariableName(parts[index]))
            {
                error = $"Invalid variable name '{parts[index]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
                return true;
            }

            index++;
        }

        if (variableCount is not 0 and not 2 and not 3)
        {
            error = "Invalid pixelsearch syntax. Expected either no variables, x_var y_var, or found_var x_var y_var before options.";
            return true;
        }

        var hasTolerance = false;
        var hasTimeout = false;
        while (index < parts.Count)
        {
            if (IsScreenReadTimeoutKeyword(parts[index]))
            {
                if (hasTimeout || index + 1 >= parts.Count || !int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs) || timeoutMs < 0)
                {
                    error = "Invalid pixelsearch timeout. Expected timeout <milliseconds>=0+.";
                    return true;
                }

                hasTimeout = true;
                index += 2;
                continue;
            }

            if (IsPixelSearchToleranceKeyword(parts[index]))
            {
                if (hasTolerance || index + 1 >= parts.Count || !int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tolerance) || tolerance is < 0 or > byte.MaxValue)
                {
                    error = "Invalid pixelsearch tolerance. Expected tolerance <0..255>.";
                    return true;
                }

                hasTolerance = true;
                index += 2;
                continue;
            }

            error = $"Unknown pixelsearch option '{parts[index]}'. Expected timeout <milliseconds>=0+ or tolerance <0..255>.";
            return true;
        }

        return true;
    }

    private static bool TryValidateImageSearchStep(IReadOnlyList<string> parts, out string? error)
    {
        error = null;
        if (parts.Count < 2)
        {
            error = "Invalid imagesearch syntax. Expected: imagesearch [<x1> <y1> <x2> <y2>] <ImageName> [found_var x_var y_var] [similarity <0..1>] [downsample <integer>=1+].";
            return true;
        }

        var imageNameIndex = 1;
        if (parts.Count >= 6 && AreIntegerTokens(parts[1], parts[2], parts[3], parts[4]))
        {
            if (!TryValidatePositiveRegion(parts, out error))
            {
                return true;
            }

            imageNameIndex = 5;
        }
        else if (parts.Count >= 5 && LooksLikeImageSearchRegion(parts))
        {
            error = "Invalid imagesearch bounds. Expected integer x1 y1 x2 y2.";
            return true;
        }

        if (!IsValidImageName(parts[imageNameIndex]))
        {
            error = $"Invalid image name '{parts[imageNameIndex]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            return true;
        }

        var index = imageNameIndex + 1;
        var variableCount = 0;
        while (index < parts.Count && !IsImageSearchOptionKeyword(parts[index]))
        {
            variableCount++;
            if (variableCount > 3)
            {
                error = "Invalid imagesearch syntax. Expected either no variables or found_var x_var y_var before options.";
                return true;
            }

            if (!EditorActionScriptTokens.IsValidVariableName(parts[index]))
            {
                error = $"Invalid variable name '{parts[index]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
                return true;
            }

            index++;
        }

        if (variableCount is not 0 and not 3)
        {
            error = "Invalid imagesearch syntax. Expected either no variables or found_var x_var y_var before options.";
            return true;
        }

        var hasSimilarity = false;
        var hasDownsample = false;
        var hasTimeout = false;
        while (index < parts.Count)
        {
            if (RunScriptSyntax.IsImageSearchSimilarityKeyword(parts[index]))
            {
                if (hasSimilarity)
                {
                    error = "Duplicate imagesearch similarity option.";
                    return true;
                }

                if (index + 1 >= parts.Count)
                {
                    error = "Invalid imagesearch similarity. Expected similarity <0..1>.";
                    return true;
                }

                if (!double.TryParse(parts[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var similarity)
                    || !double.IsFinite(similarity)
                    || similarity is < 0.0 or > 1.0)
                {
                    error = "Invalid imagesearch similarity. Expected number between 0.0 and 1.0.";
                    return true;
                }

                hasSimilarity = true;
                index += 2;
                continue;
            }

            if (RunScriptSyntax.IsImageSearchDownsampleKeyword(parts[index]))
            {
                if (hasDownsample)
                {
                    error = "Duplicate imagesearch downsample option.";
                    return true;
                }

                if (index + 1 >= parts.Count)
                {
                    error = "Invalid imagesearch downsample. Expected downsample <integer>=1+.";
                    return true;
                }

                if (!int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var downsample)
                    || downsample < 1)
                {
                    error = "Invalid imagesearch downsample. Expected integer >= 1.";
                    return true;
                }

                hasDownsample = true;
                index += 2;
                continue;
            }

            if (RunScriptPlatformSyntax.IsImageSearchMatchModeKeyword(parts[index]))
            {
                if (index + 1 >= parts.Count || !RunScriptPlatformSyntax.TryParseImageMatchMode(parts[index + 1], out _))
                {
                    error = "Invalid imagesearch matchmode. Expected matchmode <first|best>.";
                    return true;
                }

                index += 2;
                continue;
            }

            if (RunScriptSyntax.IsImageSearchScaleAwareKeyword(parts[index]))
            {
                index++;
                continue;
            }

            if (RunScriptSyntax.IsImageSearchTimeoutKeyword(parts[index]))
            {
                if (hasTimeout || index + 1 >= parts.Count)
                {
                    error = "Invalid imagesearch timeout. Expected timeout <milliseconds>=0+.";
                    return true;
                }

                if (!int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs) || timeoutMs < 0)
                {
                    error = "Invalid imagesearch timeout. Expected integer >= 0.";
                    return true;
                }

                hasTimeout = true;
                index += 2;
                continue;
            }

            error = $"Unknown imagesearch option '{parts[index]}'. Expected timeout <milliseconds>=0+, similarity <0..1>, or downsample <integer>=1+.";
            return true;
        }

        return true;
    }

    private static bool TryValidateImageClickStep(IReadOnlyList<string> parts, out string? error)
    {
        error = null;
        if (!TryValidateImageCommandPrefix(parts, RunScriptSyntax.ImageClickCommand, out var imageNameIndex, out error))
        {
            return true;
        }

        var index = imageNameIndex + 1;
        var variableCount = 0;
        while (index < parts.Count && !IsImageClickOptionKeyword(parts[index]))
        {
            variableCount++;
            if (variableCount > 3)
            {
                error = "Invalid imageclick syntax. Expected either no variables or found_var x_var y_var before options.";
                return true;
            }

            if (!EditorActionScriptTokens.IsValidVariableName(parts[index]))
            {
                error = $"Invalid variable name '{parts[index]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
                return true;
            }

            index++;
        }

        if (variableCount is not 0 and not 3)
        {
            error = "Invalid imageclick syntax. Expected either no variables or found_var x_var y_var before options.";
            return true;
        }

        var hasButton = false;
        while (index < parts.Count)
        {
            if (string.Equals(parts[index], "button", StringComparison.OrdinalIgnoreCase))
            {
                if (hasButton || index + 1 >= parts.Count || !IsValidMouseButton(parts[index + 1]))
                {
                    error = "Invalid imageclick button. Expected button <left|right|middle>.";
                    return true;
                }

                hasButton = true;
                index += 2;
                continue;
            }

            if (!TryValidateImageMatchOption(parts, ref index, out error))
            {
                return true;
            }
        }

        return true;
    }

    private static bool IsImageClickOptionKeyword(string value)
    {
        return IsImageSearchOptionKeyword(value)
            || string.Equals(value, "button", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryValidateWaitImageStep(IReadOnlyList<string> parts, out string? error)
    {
        error = null;
        if (!TryValidateImageCommandPrefix(parts, RunScriptSyntax.WaitImageCommand, out var imageNameIndex, out error))
        {
            return true;
        }

        var index = imageNameIndex + 1;
        var variableCount = 0;
        while (index < parts.Count && !IsImageSearchOptionKeyword(parts[index]))
        {
            variableCount++;
            if (variableCount > 3)
            {
                error = "Invalid waitimage syntax. Expected either no variables or found_var x_var y_var before options.";
                return true;
            }

            if (!EditorActionScriptTokens.IsValidVariableName(parts[index]))
            {
                error = $"Invalid variable name '{parts[index]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
                return true;
            }

            index++;
        }

        if (variableCount is not 0 and not 3)
        {
            error = "Invalid waitimage syntax. Expected either no variables or found_var x_var y_var before options.";
            return true;
        }

        var hasTimeout = false;
        while (index < parts.Count)
        {
            if (RunScriptSyntax.IsImageSearchTimeoutKeyword(parts[index]))
            {
                if (hasTimeout || index + 1 >= parts.Count)
                {
                    error = "Invalid waitimage timeout. Expected timeout <milliseconds>=0+.";
                    return true;
                }

                if (!int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs) || timeoutMs < 0)
                {
                    error = "Invalid waitimage timeout. Expected integer >= 0.";
                    return true;
                }

                hasTimeout = true;
                index += 2;
                continue;
            }

            if (!TryValidateImageMatchOption(parts, ref index, out error))
            {
                return true;
            }
        }

        return true;
    }

    private static bool TryValidateImageCommandPrefix(
        IReadOnlyList<string> parts,
        string commandName,
        out int imageNameIndex,
        out string? error)
    {
        error = null;
        imageNameIndex = 1;
        if (parts.Count < 2)
        {
            error = $"Invalid {commandName} syntax. Expected: {commandName} [<x1> <y1> <x2> <y2>] <ImageName> [options].";
            return false;
        }

        if (parts.Count >= 6 && AreIntegerTokens(parts[1], parts[2], parts[3], parts[4]))
        {
            if (!TryValidatePositiveRegion(parts, out error))
            {
                return false;
            }

            imageNameIndex = 5;
        }
        else if (parts.Count >= 5 && LooksLikeImageSearchRegion(parts))
        {
            error = $"Invalid {commandName} bounds. Expected integer x1 y1 x2 y2.";
            return false;
        }

        if (!IsValidImageName(parts[imageNameIndex]))
        {
            error = $"Invalid image name '{parts[imageNameIndex]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            return false;
        }

        return true;
    }

    private static bool TryValidateImageMatchOption(IReadOnlyList<string> parts, ref int index, out string? error)
    {
        error = null;
        if (RunScriptSyntax.IsImageSearchSimilarityKeyword(parts[index]))
        {
            if (index + 1 >= parts.Count
                || !double.TryParse(parts[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var similarity)
                || !double.IsFinite(similarity)
                || similarity is < 0.0 or > 1.0)
            {
                error = "Invalid image similarity. Expected similarity <0..1>.";
                return false;
            }

            index += 2;
            return true;
        }

        if (RunScriptSyntax.IsImageSearchDownsampleKeyword(parts[index]))
        {
            if (index + 1 >= parts.Count
                || !int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var downsample)
                || downsample < 1)
            {
                error = "Invalid image downsample. Expected downsample <integer>=1+.";
                return false;
            }

            index += 2;
            return true;
        }

        if (RunScriptPlatformSyntax.IsImageSearchMatchModeKeyword(parts[index]))
        {
            if (index + 1 >= parts.Count || !RunScriptPlatformSyntax.TryParseImageMatchMode(parts[index + 1], out _))
            {
                error = "Invalid image matchmode. Expected matchmode <first|best>.";
                return false;
            }

            index += 2;
            return true;
        }

        if (RunScriptSyntax.IsImageSearchTimeoutKeyword(parts[index]))
        {
            if (index + 1 >= parts.Count
                || !int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs)
                || timeoutMs < 0)
            {
                error = "Invalid image timeout. Expected timeout <milliseconds>=0+.";
                return false;
            }

            index += 2;
            return true;
        }

        if (RunScriptSyntax.IsImageSearchScaleAwareKeyword(parts[index]))
        {
            index++;
            return true;
        }

        error = $"Unknown image option '{parts[index]}'. Expected timeout <milliseconds>=0+, similarity <0..1>, or downsample <integer>=1+.";
        return false;
    }

    private static bool IsValidMouseButton(string value)
    {
        return string.Equals(value, "left", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "right", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "middle", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryValidatePositiveRegion(IReadOnlyList<string> parts, out string? error)
    {
        error = null;
        var x1 = int.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture);
        var y1 = int.Parse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture);
        var x2 = int.Parse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture);
        var y2 = int.Parse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture);
        var width = (long)x2 - x1;
        var height = (long)y2 - y1;
        if (width <= 0 || height <= 0)
        {
            error = "Invalid imagesearch bounds. Expected end-exclusive x2 and y2 to produce a positive region.";
            return false;
        }

        if (width > int.MaxValue || height > int.MaxValue)
        {
            error = "Invalid imagesearch bounds. Endpoint exceeds the supported screen coordinate range.";
            return false;
        }

        return true;
    }

    private static bool LooksLikeImageSearchRegion(IReadOnlyList<string> parts)
    {
        return parts.Count >= 6
            && (IsIntegerToken(parts[1])
                || IsIntegerToken(parts[2])
                || IsIntegerToken(parts[3])
                || IsIntegerToken(parts[4]));
    }

    private static bool IsValidImageName(string value)
    {
        return !value.StartsWith("$", StringComparison.Ordinal)
            && EditorActionScriptTokens.IsValidVariableName(value);
    }

    private static bool AreIntegerTokens(params string[] tokens)
    {
        return tokens.All(IsIntegerToken);
    }

    private static bool IsIntegerToken(string token)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsValidTargetColorToken(string token, out string? error)
    {
        error = null;
        if (ScreenPixelColor.TryParse(token, out _))
        {
            return true;
        }

        if (token.StartsWith("$", StringComparison.Ordinal))
        {
            if (EditorActionScriptTokens.IsValidVariableName(token))
            {
                return true;
            }

            error = $"Invalid variable name '{token}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            return false;
        }

        error = "Invalid color. Expected 6 hexadecimal RGB characters (RRGGBB) or $variable.";
        return false;
    }
}
