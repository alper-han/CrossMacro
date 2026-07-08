using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Shared run-script command tokens and small syntax helpers.
/// </summary>
public static class RunScriptSyntax
{
    public readonly record struct ScreenshotStep(
        string? OutputPath,
        bool CopyToClipboard,
        bool UseRegion,
        string RegionX,
        string RegionY,
        string RegionWidth,
        string RegionHeight);

    public const string ElseBlockHeader = "else {";
    public const string BlockEndToken = "}";
    public const string BreakCommand = "break";
    public const string ContinueCommand = "continue";
    public const string CurrentPositionToken = "current";
    public const string PixelColorCommand = "pixelcolor";
    public const string WaitColorCommand = "waitcolor";
    public const string PixelSearchCommand = "pixelsearch";
    public const string PixelSearchToleranceKeyword = "tolerance";
    public const string WindowCommand = "window";
    public const string ClipboardCommand = "clipboard";
    public const string ShellCommand = "shell";
    public const string ScreenshotCommand = "screenshot";

    public static List<string> SplitQuotedTokens(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var tokens = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        var tokenStarted = false;

        for (var index = 0; index < input.Length; index++)
        {
            var character = input[index];

            if (character == '\\'
                && quote is not null
                && index + 1 < input.Length
                && input[index + 1] is '"' or '\'' or '\\')
            {
                tokenStarted = true;
                current.Append(input[++index]);
                continue;
            }

            if (quote is null && char.IsWhiteSpace(character))
            {
                if (tokenStarted)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    tokenStarted = false;
                }

                continue;
            }

            if (character is '"' or '\'')
            {
                tokenStarted = true;
                if (quote == character)
                {
                    quote = null;
                    continue;
                }

                if (quote is null)
                {
                    quote = character;
                    continue;
                }
            }

            tokenStarted = true;
            current.Append(character);
        }

        if (quote is not null)
        {
            throw new FormatException("Unterminated quoted token.");
        }

        if (tokenStarted)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private static readonly string[] ScreenReadingCommands =
    [
        PixelColorCommand,
        WaitColorCommand,
        PixelSearchCommand
    ];

    public static bool IsBreakCommand(string step)
    {
        return string.Equals(step?.Trim(), BreakCommand, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsContinueCommand(string step)
    {
        return string.Equals(step?.Trim(), ContinueCommand, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBlockEndToken(string step)
    {
        return string.Equals(step?.Trim(), BlockEndToken, StringComparison.Ordinal);
    }

    public static bool IsElseHeader(string step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2
            && string.Equals(parts[0], "else", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[1], "{", StringComparison.Ordinal);
    }

    public static bool IsCurrentPositionToken(string token)
    {
        return string.Equals(token?.Trim(), CurrentPositionToken, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsScreenReadingStep(string? step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        var trimmedStep = step.TrimStart();
        foreach (var command in ScreenReadingCommands)
        {
            if (StartsWithCommandToken(trimmedStep, command))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsScreenReadingCommandToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var trimmedToken = token.Trim();
        foreach (var command in ScreenReadingCommands)
        {
            if (string.Equals(trimmedToken, command, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsPixelSearchToleranceKeyword(string? token)
    {
        return string.Equals(token?.Trim(), PixelSearchToleranceKeyword, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWindowStep(string? step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        return StartsWithCommandToken(step.TrimStart(), WindowCommand);
    }

    public static bool IsClipboardStep(string? step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        return StartsWithCommandToken(step.TrimStart(), ClipboardCommand);
    }

    public static bool IsShellStep(string? step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        return StartsWithCommandToken(step.TrimStart(), ShellCommand);
    }

    public static bool IsScreenshotStep(string? step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        return StartsWithCommandToken(step.TrimStart(), ScreenshotCommand);
    }

    public static string? ValidateScreenshotStep(string step)
    {
        return TryParseScreenshotStep(step, out _, out var error) ? null : error;
    }

    public static bool TryParseScreenshotStep(string step, out ScreenshotStep screenshotStep, out string? error)
    {
        screenshotStep = default;
        error = null;

        var index = 0;
        if (!TryReadWord(step, ref index, out var command)
            || !string.Equals(command, ScreenshotCommand, StringComparison.OrdinalIgnoreCase))
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

                if (!ValidateRegionToken(regionX, allowNegativeLiteral: false, out error)
                    || !ValidateRegionToken(regionY, allowNegativeLiteral: false, out error)
                    || !ValidateRegionToken(regionWidth, allowNegativeLiteral: true, out error)
                    || !ValidateRegionToken(regionHeight, allowNegativeLiteral: true, out error))
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

        screenshotStep = new ScreenshotStep(
            outputPath,
            copyToClipboard,
            useRegion,
            regionX,
            regionY,
            regionWidth,
            regionHeight);
        return true;
    }

    public static bool StartsWithCommandToken(string step, string command)
    {
        return step.StartsWith(command, StringComparison.OrdinalIgnoreCase)
            && (step.Length == command.Length || char.IsWhiteSpace(step[command.Length]));
    }

    private static bool IsIntegerOrVariableToken(string token)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            || (token.StartsWith("$", StringComparison.Ordinal) && EditorActionScriptTokens.IsValidVariableName(token));
    }

    private static bool ValidateRegionToken(string token, bool allowNegativeLiteral, out string? error)
    {
        error = null;
        if (!IsIntegerOrVariableToken(token))
        {
            error = $"Invalid screenshot region value '{token}'. Expected integer or $variable.";
            return false;
        }

        if (!allowNegativeLiteral
            && int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value < 0)
        {
            error = $"Invalid screenshot region value '{token}'. Expected non-negative integer or $variable.";
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

        return token.StartsWith("$", StringComparison.Ordinal) && EditorActionScriptTokens.IsValidVariableName(token);
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
                    builder.Append(value[index + 1]);
                    index += 2;
                    continue;
                }

                if (value[index] == quote)
                {
                    break;
                }

                builder.Append(value[index]);
                index++;
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
