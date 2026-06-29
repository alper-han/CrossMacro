using System;

namespace CrossMacro.Core.Services;

/// <summary>
/// Shared run-script command tokens and small syntax helpers.
/// </summary>
public static class RunScriptSyntax
{
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

    public static bool StartsWithCommandToken(string step, string command)
    {
        return step.StartsWith(command, StringComparison.OrdinalIgnoreCase)
            && (step.Length == command.Length || char.IsWhiteSpace(step[command.Length]));
    }
}
