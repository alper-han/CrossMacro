namespace CrossMacro.Infrastructure.Services.Scripting;

internal static partial class RunScriptScreenReadingStepParser
{
    /// <summary>
    /// Reads the output-variable names that sit between a command's fixed operands and its options, stopping
    /// at the first option keyword. Only the counts in <paramref name="allowedCounts"/> are valid.
    /// </summary>
    private static bool TryReadOutputVariables(
        string[] parts,
        ref int index,
        Func<string, bool> isOptionKeyword,
        ReadOnlySpan<int> allowedCounts,
        string countError,
        out string? error)
    {
        error = null;
        var count = 0;
        while (index < parts.Length && !isOptionKeyword(parts[index]))
        {
            if (++count > 3)
            {
                error = countError;
                return false;
            }

            if (!ScriptIdentifierSyntax.IsValidVariableName(parts[index]))
            {
                error = ScriptMessages.InvalidVariableName(parts[index]);
                return false;
            }

            index++;
        }

        if (!allowedCounts.Contains(count))
        {
            error = countError;
            return false;
        }

        return true;
    }

    private static bool TryReadNonNegativeInteger(string[] parts, int index, out int value)
    {
        value = 0;
        return index < parts.Length
            && int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value >= 0;
    }

    private static bool TryReadSimilarity(string[] parts, int index) =>
        index < parts.Length
        && double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var similarity)
        && double.IsFinite(similarity)
        && similarity is >= 0.0 and <= 1.0;

    private static bool AreIntegerTokens(params ReadOnlySpan<string> tokens)
    {
        foreach (var token in tokens)
        {
            if (!IsIntegerToken(token))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIntegerToken(string token)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsIntegerOrVariableToken(string token)
    {
        return IsIntegerToken(token)
            || (token.StartsWith('$') && ScriptIdentifierSyntax.IsValidVariableName(token));
    }

    private static bool IsPositiveIntegerOrVariableToken(string token)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value > 0
            : token.StartsWith('$') && ScriptIdentifierSyntax.IsValidVariableName(token);
    }

    private static bool IsValidMouseButton(string value)
    {
        return string.Equals(value, "left", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "right", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "middle", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidImageName(string value)
    {
        return !value.StartsWith('$')
            && ScriptIdentifierSyntax.IsValidVariableName(value);
    }

    private static bool IsValidTargetColorToken(string token, out string? error)
    {
        error = null;
        if (ScreenPixelColor.TryParse(token, out _))
        {
            return true;
        }

        if (!token.StartsWith('$'))
        {
            error = "Invalid color. Expected 6 hexadecimal RGB characters (RRGGBB) or $variable.";
            return false;
        }

        if (ScriptIdentifierSyntax.IsValidVariableName(token))
        {
            return true;
        }

        error = ScriptMessages.InvalidVariableName(token);
        return false;
    }
}
