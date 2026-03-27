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
}
