namespace CrossMacro.Infrastructure.Services.Scripting;

internal static partial class RunScriptScreenReadingStepParser
{
    private static string ImageVariableCountError(string commandName) =>
        $"Invalid {commandName} syntax. Expected either no variables or found_var x_var y_var before options.";

    public static bool IsExplicitImageRegion(string[] parts)
    {
        return parts.Length >= 7
            && string.Equals(parts[1], "region", StringComparison.OrdinalIgnoreCase)
            && IsIntegerOrVariableToken(parts[2]);
    }

    private static bool IsImageClickOptionKeyword(string value)
    {
        return IsImageSearchOptionKeyword(value)
            || string.Equals(value, "button", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryValidateImageSearchStep(string[] parts, out string? error)
    {
        const string command = RunScriptSyntax.ImageSearchCommand;
        if (!TryValidateImageCommandPrefix(parts, command, out var imageNameIndex, out error))
        {
            return true;
        }

        var index = imageNameIndex + 1;
        if (!TryReadOutputVariables(parts, ref index, IsImageSearchOptionKeyword, [0, 3], ImageVariableCountError(command), out error))
        {
            return true;
        }

        var hasSimilarity = false;
        var hasMatchMode = false;
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

                if (!TryReadSimilarity(parts, index + 1))
                {
                    error = "Invalid imagesearch similarity. Expected number between 0.0 and 1.0.";
                    return true;
                }

                hasSimilarity = true;
            }
            else if (RunScriptPlatformSyntax.IsImageSearchMatchModeKeyword(parts[index]))
            {
                if (hasMatchMode)
                {
                    error = "Duplicate imagesearch matchmode option.";
                    return true;
                }

                if (index + 1 >= parts.Length || !RunScriptPlatformSyntax.TryParseImageMatchMode(parts[index + 1], out _))
                {
                    error = "Invalid imagesearch matchmode. Expected matchmode <auto|first|best>.";
                    return true;
                }

                hasMatchMode = true;
            }
            else
            {
                error = $"Unknown imagesearch option '{parts[index]}'. Expected similarity <0..1> or matchmode <auto|first|best>.";
                return true;
            }

            index += 2;
        }

        return true;
    }

    private static bool TryValidateImageClickStep(string[] parts, out string? error)
    {
        const string command = RunScriptSyntax.ImageClickCommand;
        if (!TryValidateImageCommandPrefix(parts, command, out var imageNameIndex, out error))
        {
            return true;
        }

        var index = imageNameIndex + 1;
        if (!TryReadOutputVariables(parts, ref index, IsImageClickOptionKeyword, [0, 3], ImageVariableCountError(command), out error))
        {
            return true;
        }

        var hasButton = false;
        var seenOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (index < parts.Length)
        {
            if (string.Equals(parts[index], "button", StringComparison.OrdinalIgnoreCase))
            {
                if (hasButton || index + 1 >= parts.Length || !IsValidMouseButton(parts[index + 1]))
                {
                    error = "Invalid imageclick button. Expected button <left|right|middle>.";
                    return true;
                }

                hasButton = true;
                index += 2;
            }
            else if (!TryValidateImageMatchOption(parts, ref index, seenOptions, out error))
            {
                return true;
            }
        }

        return true;
    }

    private static bool TryValidateWaitImageStep(string[] parts, out string? error)
    {
        const string command = RunScriptSyntax.WaitImageCommand;
        if (!TryValidateImageCommandPrefix(parts, command, out var imageNameIndex, out error))
        {
            return true;
        }

        var index = imageNameIndex + 1;
        if (!TryReadOutputVariables(parts, ref index, IsImageSearchOptionKeyword, [0, 3], ImageVariableCountError(command), out error))
        {
            return true;
        }

        var hasTimeout = false;
        var seenOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (index < parts.Length)
        {
            if (RunScriptSyntax.IsImageSearchTimeoutKeyword(parts[index]))
            {
                if (hasTimeout || index + 1 >= parts.Length)
                {
                    error = "Invalid waitimage timeout. Expected timeout <milliseconds>=0+.";
                    return true;
                }

                if (!TryReadNonNegativeInteger(parts, index + 1, out _))
                {
                    error = "Invalid waitimage timeout. Expected integer >= 0.";
                    return true;
                }

                hasTimeout = true;
                index += 2;
            }
            else if (!TryValidateImageMatchOption(parts, ref index, seenOptions, out error))
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
            error = $"Invalid {commandName} syntax. Expected: {commandName} [<x1> <y1> <x2> <y2>|region <left> <top> <width> <height>] <ImageName> [options].";
            return false;
        }

        if (IsExplicitImageRegion(parts))
        {
            if (!TryValidateExplicitImageRegion(parts, commandName, out error))
            {
                return false;
            }

            imageNameIndex = 6;
        }
        else if (parts.Length >= 6 && AreIntegerTokens(parts[1], parts[2], parts[3], parts[4]))
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

    private static bool TryValidateExplicitImageRegion(string[] parts, string commandName, out string? error)
    {
        error = null;
        if (parts.Length < 7)
        {
            error = $"Invalid {commandName} region. Expected: {commandName} region <left> <top> <width> <height> <ImageName> [options].";
            return false;
        }

        if (!IsIntegerOrVariableToken(parts[2])
            || !IsIntegerOrVariableToken(parts[3])
            || !IsPositiveIntegerOrVariableToken(parts[4])
            || !IsPositiveIntegerOrVariableToken(parts[5]))
        {
            error = $"Invalid {commandName} region. Expected integer or $variable left/top and positive integer or $variable width/height.";
            return false;
        }

        if (int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var left)
            && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var top)
            && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
            && int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
            && ((long)left + width > int.MaxValue || (long)top + height > int.MaxValue))
        {
            error = $"Invalid {commandName} region. Endpoint exceeds the supported screen coordinate range.";
            return false;
        }

        return true;
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

    // An option value can be numeric. Only the first operand selects legacy-region syntax.
    private static bool LooksLikeImageSearchRegion(string[] parts)
    {
        return parts.Length >= 6 && IsIntegerToken(parts[1]);
    }

    /// <summary>Validates one option shared by <c>imageclick</c> and <c>waitimage</c>, each allowed at most once.</summary>
    private static bool TryValidateImageMatchOption(
        string[] parts,
        ref int index,
        ISet<string> seenOptions,
        out string? error)
    {
        error = null;
        var option = parts[index];
        if (RunScriptSyntax.IsImageSearchSimilarityKeyword(option))
        {
            if (!seenOptions.Add("similarity"))
            {
                error = "Duplicate image similarity option.";
                return false;
            }

            if (!TryReadSimilarity(parts, index + 1))
            {
                error = "Invalid image similarity. Expected similarity <0..1>.";
                return false;
            }
        }
        else if (RunScriptPlatformSyntax.IsImageSearchMatchModeKeyword(option))
        {
            if (!seenOptions.Add("matchmode"))
            {
                error = "Duplicate image matchmode option.";
                return false;
            }

            if (index + 1 >= parts.Length || !RunScriptPlatformSyntax.TryParseImageMatchMode(parts[index + 1], out _))
            {
                error = "Invalid image matchmode. Expected matchmode <auto|first|best>.";
                return false;
            }
        }
        else if (RunScriptSyntax.IsImageSearchTimeoutKeyword(option))
        {
            if (!seenOptions.Add("timeout"))
            {
                error = "Duplicate image timeout option.";
                return false;
            }

            if (!TryReadNonNegativeInteger(parts, index + 1, out _))
            {
                error = "Invalid image timeout. Expected timeout <milliseconds>=0+.";
                return false;
            }
        }
        else
        {
            error = $"Unknown image option '{option}'. Expected timeout <milliseconds>=0+, similarity <0..1>, or matchmode <auto|first|best>.";
            return false;
        }

        index += 2;
        return true;
    }
}
