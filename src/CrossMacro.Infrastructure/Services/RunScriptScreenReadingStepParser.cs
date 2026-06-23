using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

internal enum RunScriptScreenReadingCommand
{
    PixelColor,
    WaitColor,
    PixelSearch
}

internal readonly record struct PixelSearchVariableLayout(
    string? FoundVariableName,
    string? XVariableName,
    string? YVariableName);

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
            _ => false
        };
    }

    public static bool TryParseCommand(
        string step,
        out RunScriptScreenReadingCommand command,
        out string[] parts)
    {
        command = default;
        parts = SplitStep(step);
        if (parts.Length == 0)
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

        return false;
    }

    public static string[] SplitStep(string step)
    {
        return step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static PixelSearchVariableLayout GetPixelSearchVariableLayout(IReadOnlyList<string> parts)
    {
        if (parts.Count >= 9 && !IsPixelSearchToleranceKeyword(parts[6]) && !IsPixelSearchToleranceKeyword(parts[8]))
        {
            return new PixelSearchVariableLayout(parts[6], parts[7], parts[8]);
        }

        if (parts.Count >= 8 && !IsPixelSearchToleranceKeyword(parts[6]))
        {
            return new PixelSearchVariableLayout(null, parts[6], parts[7]);
        }

        return default;
    }

    public static bool IsPixelSearchToleranceKeyword(string value) =>
        RunScriptSyntax.IsPixelSearchToleranceKeyword(value);

    private static bool TryValidatePixelColorStep(IReadOnlyList<string> parts, out string? error)
    {
        error = null;
        var isRelative = parts.Count > 1 && string.Equals(parts[1], "rel", StringComparison.OrdinalIgnoreCase);
        var coordinateIndex = isRelative ? 2 : 1;
        var expectedLengthWithoutVariable = isRelative ? 4 : 3;
        var expectedLengthWithVariable = expectedLengthWithoutVariable + 1;
        if (parts.Count != expectedLengthWithoutVariable && parts.Count != expectedLengthWithVariable)
        {
            error = isRelative
                ? "Invalid pixelcolor syntax. Expected: pixelcolor rel <dx> <dy> [var]."
                : "Invalid pixelcolor syntax. Expected: pixelcolor <x> <y> [var] or pixelcolor rel <dx> <dy> [var].";
            return true;
        }

        if (!AreIntegerTokens(parts[coordinateIndex], parts[coordinateIndex + 1]))
        {
            error = isRelative
                ? "Invalid pixelcolor coordinate. Expected integer dx and dy."
                : "Invalid pixelcolor coordinate. Expected integer x and y.";
            return true;
        }

        if (parts.Count == expectedLengthWithVariable && !EditorActionScriptTokens.IsValidVariableName(parts[^1]))
        {
            error = $"Invalid variable name '{parts[^1]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
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

        if (parts.Count == 6 && !EditorActionScriptTokens.IsValidVariableName(parts[5]))
        {
            error = $"Invalid variable name '{parts[5]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
        }

        return true;
    }

    private static bool TryValidatePixelSearchStep(IReadOnlyList<string> parts, out string? error)
    {
        error = null;
        if (parts.Count != 6 && parts.Count != 8 && parts.Count != 9 && parts.Count != 10 && parts.Count != 11)
        {
            error = "Invalid pixelsearch syntax. Expected: pixelsearch <x1> <y1> <x2> <y2> <color> [found_var var_x var_y|var_x var_y] [tolerance <0..255>].";
            return true;
        }

        if (!AreIntegerTokens(parts[1], parts[2], parts[3], parts[4]))
        {
            error = "Invalid pixelsearch bounds. Expected integer x1 y1 x2 y2.";
            return true;
        }

        if (!IsValidTargetColorToken(parts[5], out error))
        {
            return true;
        }

        var hasTolerance = false;
        var variableStartIndex = 6;
        var toleranceKeywordIndex = -1;

        if (parts.Count == 8 && IsPixelSearchToleranceKeyword(parts[6]))
        {
            hasTolerance = true;
            toleranceKeywordIndex = 6;
        }
        else if (parts.Count is 10 or 11)
        {
            hasTolerance = true;
            toleranceKeywordIndex = parts.Count == 11 ? 9 : 8;
        }

        if (parts.Count is 8 or 9 or 10 or 11)
        {
            var variableCount = hasTolerance && toleranceKeywordIndex == 6
                ? 0
                : parts.Count is 9 or 11 ? 3 : 2;
            for (var offset = 0; offset < variableCount; offset++)
            {
                if (!EditorActionScriptTokens.IsValidVariableName(parts[variableStartIndex + offset]))
                {
                    error = $"Invalid variable name '{parts[variableStartIndex + offset]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
                    return true;
                }
            }
        }

        if (hasTolerance)
        {
            if (!IsPixelSearchToleranceKeyword(parts[toleranceKeywordIndex]))
            {
                error = "Invalid pixelsearch tolerance. Expected 'tolerance <0..255>'.";
                return true;
            }

            if (!int.TryParse(parts[toleranceKeywordIndex + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tolerance)
                || tolerance is < 0 or > byte.MaxValue)
            {
                error = "Invalid pixelsearch tolerance. Expected integer between 0 and 255.";
            }
        }

        return true;
    }

    private static bool AreIntegerTokens(params string[] tokens)
    {
        return tokens.All(token => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
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
