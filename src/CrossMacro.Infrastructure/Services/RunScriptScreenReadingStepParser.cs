
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
            parts = [];
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

    public static PixelSearchVariableLayout GetPixelSearchVariableLayout(string[] parts)
    {
        if (parts.Length >= 9 && !IsPixelSearchOptionKeyword(parts[6]) && !IsPixelSearchOptionKeyword(parts[8]))
        {
            return new PixelSearchVariableLayout(parts[6], parts[7], parts[8]);
        }

        if (parts.Length >= 8 && !IsPixelSearchOptionKeyword(parts[6]))
        {
            return new PixelSearchVariableLayout(FoundVariableName: null, parts[6], parts[7]);
        }

        return default;
    }

    public static bool IsPixelSearchToleranceKeyword(string value) =>
        RunScriptSyntax.IsPixelSearchToleranceKeyword(value);

    public static bool IsScreenReadTimeoutKeyword(string value) =>
        RunScriptSyntax.IsImageSearchTimeoutKeyword(value);

    public static bool IsScreenReadPollKeyword(string value) =>
        RunScriptSyntax.IsScreenReadPollKeyword(value);

    public static bool IsPixelSearchOptionKeyword(string value) =>
        ScreenReadOptionGrammar.IsPixelSearchOption(ScreenReadOptionGrammar.GetScriptOptionKind(value));

    public static bool IsImageSearchOptionKeyword(string value) =>
        ScreenReadOptionGrammar.IsImageSearchOption(ScreenReadOptionGrammar.GetScriptOptionKind(value));

    public static bool IsImageMatchOptionKeyword(string value) =>
        ScreenReadOptionGrammar.IsImageMatchOption(ScreenReadOptionGrammar.GetScriptOptionKind(value));

    private static bool TryValidatePixelColorStep(string[] parts, out string? error)
    {
        error = null;
        var isRelative = parts.Length > 1 && string.Equals(parts[1], "rel", StringComparison.OrdinalIgnoreCase);
        var coordinateIndex = isRelative ? 2 : 1;
        var optionStartIndex = isRelative ? 4 : 3;
        if (parts.Length < optionStartIndex)
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

        if (parts.Length > optionStartIndex && !IsScreenReadTimeoutKeyword(parts[optionStartIndex]))
        {
            if (!EditorActionScriptTokens.IsValidVariableName(parts[optionStartIndex]))
            {
                error = $"Invalid variable name '{parts[optionStartIndex]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
                return true;
            }

            optionStartIndex++;
        }

        var hasTimeout = false;
        while (optionStartIndex < parts.Length)
        {
            if (!IsScreenReadTimeoutKeyword(parts[optionStartIndex])
                || hasTimeout
                || optionStartIndex + 1 >= parts.Length
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

    private static bool TryValidateWaitColorStep(string[] parts, out string? error)
    {
        error = null;
        if (parts.Length < 4)
        {
            error = "Invalid waitcolor syntax. Expected: waitcolor <x> <y> <color> [timeout_ms] [result_var] [poll [interval_ms]].";
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

        var index = 4;
        if (index < parts.Length && !IsScreenReadPollKeyword(parts[index]))
        {
            if (!int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs) || timeoutMs < 0)
            {
                error = "Invalid waitcolor timeout. Expected integer >= 0.";
                return true;
            }

            index++;
        }

        if (index < parts.Length && !IsScreenReadPollKeyword(parts[index]))
        {
            if (!EditorActionScriptTokens.IsValidVariableName(parts[index]))
            {
                error = $"Invalid variable name '{parts[index]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
                return true;
            }

            index++;
        }

        if (index < parts.Length)
        {
            if (!IsScreenReadPollKeyword(parts[index]))
            {
                error = "Invalid waitcolor poll. Expected poll [<milliseconds>=1+].";
                return true;
            }

            if (!TryConsumePollOption(parts, ref index, IsScreenReadPollKeyword, out error))
            {
                error ??= "Invalid waitcolor poll. Expected poll [<milliseconds>=1+].";
                return true;
            }
        }

        if (index < parts.Length)
        {
            error = "Invalid waitcolor syntax. Expected: waitcolor <x> <y> <color> [timeout_ms] [result_var] [poll [interval_ms]].";
        }

        return true;
    }

    private static bool TryValidatePixelSearchStep(string[] parts, out string? error)
    {
        error = null;
        if (parts.Length < 6)
        {
            error = "Invalid pixelsearch syntax. Expected: pixelsearch <x1> <y1> <x2> <y2> <color> [found_var var_x var_y|var_x var_y] [timeout <milliseconds>=0+] [tolerance <0..255>] [poll [interval_ms]].";
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
        while (index < parts.Length && !IsPixelSearchOptionKeyword(parts[index]))
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
        var hasPoll = false;
        while (index < parts.Length)
        {
            if (IsScreenReadTimeoutKeyword(parts[index]))
            {
                if (hasTimeout || index + 1 >= parts.Length || !int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs) || timeoutMs < 0)
                {
                    error = "Invalid pixelsearch timeout. Expected timeout <milliseconds>=0+.";
                    return true;
                }

                hasTimeout = true;
                index += 2;
                continue;
            }

            if (IsScreenReadPollKeyword(parts[index]))
            {
                if (hasPoll || !TryConsumePollOption(parts, ref index, IsPixelSearchOptionKeyword, out error))
                {
                    error ??= "Invalid pixelsearch poll. Expected poll [<milliseconds>=1+].";
                    return true;
                }

                hasPoll = true;
                continue;
            }

            if (IsPixelSearchToleranceKeyword(parts[index]))
            {
                if (hasTolerance || index + 1 >= parts.Length || !int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tolerance) || tolerance is < 0 or > byte.MaxValue)
                {
                    error = "Invalid pixelsearch tolerance. Expected tolerance <0..255>.";
                    return true;
                }

                hasTolerance = true;
                index += 2;
                continue;
            }

            error = $"Unknown pixelsearch option '{parts[index]}'. Expected timeout <milliseconds>=0+, tolerance <0..255>, or poll [<milliseconds>=1+].";
            return true;
        }

        return true;
    }

    private static bool TryValidateImageSearchStep(string[] parts, out string? error)
    {
        error = null;
        if (parts.Length < 2)
        {
            error = "Invalid imagesearch syntax. Expected: imagesearch [<x1> <y1> <x2> <y2>] <ImageName> [found_var x_var y_var] [similarity <0..1>] [downsample <integer>=1+].";
            return true;
        }

        var imageNameIndex = 1;
        if (parts.Length >= 6 && AreIntegerTokens(parts[1], parts[2], parts[3], parts[4]))
        {
            if (!TryValidatePositiveRegion(parts, out error))
            {
                return true;
            }

            imageNameIndex = 5;
        }
        else if (parts.Length >= 5 && LooksLikeImageSearchRegion(parts))
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
        while (index < parts.Length && !IsImageSearchOptionKeyword(parts[index]))
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
        var hasPoll = false;
        var hasMatchMode = false;
        var hasScaleAware = false;
        while (index < parts.Length)
        {
            if (RunScriptSyntax.IsImageSearchSimilarityKeyword(parts[index]))
            {
                if (hasSimilarity)
                {
                    error = "Duplicate imagesearch similarity option.";
                    return true;
                }

                if (index + 1 >= parts.Length)
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

                if (index + 1 >= parts.Length)
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
                if (hasMatchMode)
                {
                    error = "Duplicate imagesearch matchmode option.";
                    return true;
                }

                if (index + 1 >= parts.Length || !RunScriptPlatformSyntax.TryParseImageMatchMode(parts[index + 1], out _))
                {
                    error = "Invalid imagesearch matchmode. Expected matchmode <first|best>.";
                    return true;
                }

                hasMatchMode = true;
                index += 2;
                continue;
            }

            if (RunScriptSyntax.IsImageSearchScaleAwareKeyword(parts[index]))
            {
                if (hasScaleAware)
                {
                    error = "Duplicate imagesearch scale-aware option.";
                    return true;
                }

                hasScaleAware = true;
                index++;
                continue;
            }

            if (RunScriptSyntax.IsImageSearchTimeoutKeyword(parts[index]))
            {
                if (hasTimeout || index + 1 >= parts.Length)
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

            if (IsScreenReadPollKeyword(parts[index]))
            {
                if (hasPoll || !TryConsumePollOption(parts, ref index, IsImageSearchOptionKeyword, out error))
                {
                    error ??= "Invalid imagesearch poll. Expected poll [<milliseconds>=1+].";
                    return true;
                }

                hasPoll = true;
                continue;
            }

            error = $"Unknown imagesearch option '{parts[index]}'. Expected timeout <milliseconds>=0+, poll [<milliseconds>=1+], similarity <0..1>, or downsample <integer>=1+.";
            return true;
        }

        return true;
    }

    private static bool TryValidateImageClickStep(string[] parts, out string? error)
    {
        error = null;
        if (!TryValidateImageCommandPrefix(parts, RunScriptSyntax.ImageClickCommand, out var imageNameIndex, out error))
        {
            return true;
        }

        var index = imageNameIndex + 1;
        var variableCount = 0;
        while (index < parts.Length && !IsImageClickOptionKeyword(parts[index]))
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
        var hasPoll = false;
        var seenOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (index < parts.Length)
        {
            if (IsScreenReadPollKeyword(parts[index]))
            {
                if (hasPoll || !TryConsumePollOption(parts, ref index, IsImageClickOptionKeyword, out error))
                {
                    error ??= "Invalid imageclick poll. Expected poll [<milliseconds>=1+].";
                    return true;
                }

                hasPoll = true;
                continue;
            }

            if (string.Equals(parts[index], "button", StringComparison.OrdinalIgnoreCase))
            {
                if (hasButton || index + 1 >= parts.Length || !IsValidMouseButton(parts[index + 1]))
                {
                    error = "Invalid imageclick button. Expected button <left|right|middle>.";
                    return true;
                }

                hasButton = true;
                index += 2;
                continue;
            }

            if (!TryValidateImageMatchOption(parts, ref index, seenOptions, out error))
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

    private static bool TryValidateWaitImageStep(string[] parts, out string? error)
    {
        error = null;
        if (!TryValidateImageCommandPrefix(parts, RunScriptSyntax.WaitImageCommand, out var imageNameIndex, out error))
        {
            return true;
        }

        var index = imageNameIndex + 1;
        var variableCount = 0;
        while (index < parts.Length && !IsImageSearchOptionKeyword(parts[index]))
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
        var hasPoll = false;
        var seenOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (index < parts.Length)
        {
            if (IsScreenReadPollKeyword(parts[index]))
            {
                if (hasPoll || !TryConsumePollOption(parts, ref index, IsImageSearchOptionKeyword, out error))
                {
                    error ??= "Invalid waitimage poll. Expected poll [<milliseconds>=1+].";
                    return true;
                }

                hasPoll = true;
                continue;
            }

            if (RunScriptSyntax.IsImageSearchTimeoutKeyword(parts[index]))
            {
                if (hasTimeout || index + 1 >= parts.Length)
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

            if (!TryValidateImageMatchOption(parts, ref index, seenOptions, out error))
            {
                return true;
            }
        }

        return true;
    }

    private static bool TryValidateImageCommandPrefix(
        string[] parts,
        string commandName,
        out int imageNameIndex,
        out string? error)
    {
        error = null;
        imageNameIndex = 1;
        if (parts.Length < 2)
        {
            error = $"Invalid {commandName} syntax. Expected: {commandName} [<x1> <y1> <x2> <y2>] <ImageName> [options].";
            return false;
        }

        if (parts.Length >= 6 && AreIntegerTokens(parts[1], parts[2], parts[3], parts[4]))
        {
            if (!TryValidatePositiveRegion(parts, out error))
            {
                return false;
            }

            imageNameIndex = 5;
        }
        else if (parts.Length >= 5 && LooksLikeImageSearchRegion(parts))
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

    private static bool TryValidateImageMatchOption(
        string[] parts,
        ref int index,
        ISet<string> seenOptions,
        out string? error)
    {
        error = null;
        if (RunScriptSyntax.IsImageSearchSimilarityKeyword(parts[index]))
        {
            if (!seenOptions.Add("similarity"))
            {
                error = "Duplicate image similarity option.";
                return false;
            }

            if (index + 1 >= parts.Length
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
            if (!seenOptions.Add("downsample"))
            {
                error = "Duplicate image downsample option.";
                return false;
            }

            if (index + 1 >= parts.Length
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
            if (!seenOptions.Add("matchmode"))
            {
                error = "Duplicate image matchmode option.";
                return false;
            }

            if (index + 1 >= parts.Length || !RunScriptPlatformSyntax.TryParseImageMatchMode(parts[index + 1], out _))
            {
                error = "Invalid image matchmode. Expected matchmode <first|best>.";
                return false;
            }

            index += 2;
            return true;
        }

        if (RunScriptSyntax.IsImageSearchTimeoutKeyword(parts[index]))
        {
            if (!seenOptions.Add("timeout"))
            {
                error = "Duplicate image timeout option.";
                return false;
            }

            if (index + 1 >= parts.Length
                || !int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs)
                || timeoutMs < 0)
            {
                error = "Invalid image timeout. Expected timeout <milliseconds>=0+.";
                return false;
            }

            index += 2;
            return true;
        }

        if (IsScreenReadPollKeyword(parts[index]))
        {
            if (!seenOptions.Add("poll"))
            {
                error = "Duplicate image poll option.";
                return false;
            }

            if (!TryConsumePollOption(parts, ref index, IsImageSearchOptionKeyword, out error))
            {
                return false;
            }

            return true;
        }

        if (RunScriptSyntax.IsImageSearchScaleAwareKeyword(parts[index]))
        {
            if (!seenOptions.Add("scale-aware"))
            {
                error = "Duplicate image scale-aware option.";
                return false;
            }

            index++;
            return true;
        }

        error = $"Unknown image option '{parts[index]}'. Expected timeout <milliseconds>=0+, poll [<milliseconds>=1+], similarity <0..1>, or downsample <integer>=1+.";
        return false;
    }

    private static bool TryConsumePollOption(
        string[] parts,
        ref int index,
        Func<string, bool> isNextOption,
        out string? error)
    {
        error = null;
        index++;
        if (index >= parts.Length || isNextOption(parts[index]))
        {
            return true;
        }

        if (!int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var intervalMs)
            || intervalMs <= 0)
        {
            error = "Invalid screen poll interval. Expected a positive integer in milliseconds.";
            return false;
        }

        index++;
        return true;
    }

    private static bool IsValidMouseButton(string value)
    {
        return string.Equals(value, "left", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "right", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "middle", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryValidatePositiveRegion(string[] parts, out string? error)
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

    private static bool LooksLikeImageSearchRegion(string[] parts)
    {
        return parts.Length >= 6
            && (IsIntegerToken(parts[1])
                || IsIntegerToken(parts[2])
                || IsIntegerToken(parts[3])
                || IsIntegerToken(parts[4]));
    }

    private static bool IsValidImageName(string value)
    {
        return !value.StartsWith('$')
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

        if (token.StartsWith('$'))
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
