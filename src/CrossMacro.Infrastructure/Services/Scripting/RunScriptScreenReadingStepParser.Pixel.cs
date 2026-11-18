namespace CrossMacro.Infrastructure.Services.Scripting;

internal static partial class RunScriptScreenReadingStepParser
{
    private const string PixelSearchVariableCountError =
        "Invalid pixelsearch syntax. Expected either no variables, x_var y_var, or found_var x_var y_var before options.";

    private static bool TryValidatePixelColorStep(string[] parts, out string? error)
    {
        error = null;
        var isRelative = parts.Length > 1 && string.Equals(parts[1], "rel", StringComparison.OrdinalIgnoreCase);
        var coordinateIndex = isRelative ? 2 : 1;
        var optionStartIndex = isRelative ? 4 : 3;
        const string syntaxError = "Invalid pixelcolor syntax. Expected: pixelcolor <x> <y> [var] or pixelcolor rel <dx> <dy> [var].";
        if (parts.Length < optionStartIndex)
        {
            error = isRelative
                ? "Invalid pixelcolor syntax. Expected: pixelcolor rel <dx> <dy> [var]."
                : syntaxError;
            return true;
        }

        if (!AreIntegerTokens(parts[coordinateIndex], parts[coordinateIndex + 1]))
        {
            error = isRelative
                ? "Invalid pixelcolor coordinate. Expected integer dx and dy."
                : "Invalid pixelcolor coordinate. Expected integer x and y.";
            return true;
        }

        if (parts.Length > optionStartIndex)
        {
            if (!ScriptIdentifierSyntax.IsValidVariableName(parts[optionStartIndex]))
            {
                error = ScriptMessages.InvalidVariableName(parts[optionStartIndex]);
                return true;
            }

            optionStartIndex++;
        }

        if (optionStartIndex < parts.Length)
        {
            error = syntaxError;
        }

        return true;
    }

    private static bool TryValidateWaitColorStep(string[] parts, out string? error)
    {
        const string syntaxError = "Invalid waitcolor syntax. Expected: waitcolor <x> <y> <color> [timeout_ms] [result_var].";
        if (parts.Length < 4)
        {
            error = syntaxError;
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
        if (index < parts.Length)
        {
            if (!TryReadNonNegativeInteger(parts, index, out _))
            {
                error = "Invalid waitcolor timeout. Expected integer >= 0.";
                return true;
            }

            index++;
        }

        if (index < parts.Length)
        {
            if (!ScriptIdentifierSyntax.IsValidVariableName(parts[index]))
            {
                error = ScriptMessages.InvalidVariableName(parts[index]);
                return true;
            }

            index++;
        }

        if (index < parts.Length)
        {
            error = syntaxError;
        }

        return true;
    }

    private static bool TryValidatePixelSearchStep(string[] parts, out string? error)
    {
        if (parts.Length < 6)
        {
            error = "Invalid pixelsearch syntax. Expected: pixelsearch <x1> <y1> <x2> <y2> <color> [found_var var_x var_y|var_x var_y] [timeout <milliseconds>=0+] [tolerance <0..255>].";
            return true;
        }

        if (!AreIntegerTokens(parts[1], parts[2], parts[3], parts[4]))
        {
            error = "Invalid pixelsearch bounds. Expected integer x1 y1 x2 y2.";
            return true;
        }

        if (SpansBeyondIntegerRange(parts[1], parts[3]) || SpansBeyondIntegerRange(parts[2], parts[4]))
        {
            error = "Invalid pixelsearch bounds. Endpoint exceeds the supported screen coordinate range.";
            return true;
        }

        if (!IsValidTargetColorToken(parts[5], out error))
        {
            return true;
        }

        var index = 6;
        if (!TryReadOutputVariables(parts, ref index, IsPixelSearchOptionKeyword, [0, 2, 3], PixelSearchVariableCountError, out error))
        {
            return true;
        }

        var hasTolerance = false;
        var hasTimeout = false;
        while (index < parts.Length)
        {
            if (IsScreenReadTimeoutKeyword(parts[index]))
            {
                if (hasTimeout || !TryReadNonNegativeInteger(parts, index + 1, out _))
                {
                    error = "Invalid pixelsearch timeout. Expected timeout <milliseconds>=0+.";
                    return true;
                }

                hasTimeout = true;
            }
            else if (IsPixelSearchToleranceKeyword(parts[index]))
            {
                if (hasTolerance || !TryReadNonNegativeInteger(parts, index + 1, out var tolerance) || tolerance > byte.MaxValue)
                {
                    error = "Invalid pixelsearch tolerance. Expected tolerance <0..255>.";
                    return true;
                }

                hasTolerance = true;
            }
            else
            {
                error = $"Unknown pixelsearch option '{parts[index]}'. Expected timeout <milliseconds>=0+ or tolerance <0..255>.";
                return true;
            }

            index += 2;
        }

        return true;
    }

    // Both tokens are validated integers; the span between two corners must itself fit an int.
    private static bool SpansBeyondIntegerRange(string first, string second)
    {
        var a = int.Parse(first, NumberStyles.Integer, CultureInfo.InvariantCulture);
        var b = int.Parse(second, NumberStyles.Integer, CultureInfo.InvariantCulture);
        return (long)Math.Max(a, b) - Math.Min(a, b) > int.MaxValue;
    }
}
