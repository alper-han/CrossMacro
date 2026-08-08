
namespace CrossMacro.Core.Services;

/// <summary>
/// Shared repeat/for block-header parser for both interpreters (compiler + executor).
/// Segments are raw 1- or 3-token expression forms, classified via <see cref="ScriptNumericExpression"/>.
/// </summary>
public static class RunScriptHeaderParser
{
    private const string ForSyntaxError = "Invalid for syntax. Expected: for <var> from <start> to <end> [step <n>] {";

    public static bool TryParseRepeatCountToken(string step, out string countToken)
    {
        countToken = string.Empty;
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        // The closing brace must be a standalone token; the count may be a 1- or 3-token expression.
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 3
            && string.Equals(parts[0], "repeat", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[^1], "{", StringComparison.Ordinal))
        {
            countToken = string.Join(' ', parts, 1, parts.Length - 2);
            return true;
        }

        return false;
    }

    public static bool TryParseForHeader(string step, out RunScriptForHeader? header, out string? error)
    {
        header = null;
        error = null;

        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        if (!step.EndsWith('{') || !step.StartsWith("for ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[..^1].Trim();
        var parts = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // `from` is fixed after the loop variable; `to`/`step` float with segment lengths; the $ sigil prevents keyword collisions.
        if (parts.Length < 5
            || !string.Equals(parts[2], "from", StringComparison.OrdinalIgnoreCase))
        {
            error = ForSyntaxError;
            return true;
        }

        var toIndex = IndexOfKeyword(parts, "to", startIndex: 3);
        if (toIndex < 0)
        {
            error = ForSyntaxError;
            return true;
        }

        var variableName = parts[1];
        if (!EditorActionScriptTokens.IsValidVariableName(variableName))
        {
            error = $"Invalid loop variable name '{variableName}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            return true;
        }

        var stepIndex = IndexOfKeyword(parts, "step", toIndex + 1);
        var startToken = JoinSegment(parts, 3, toIndex);
        var endToken = JoinSegment(parts, toIndex + 1, stepIndex >= 0 ? stepIndex : parts.Length);
        var stepToken = stepIndex >= 0 ? JoinSegment(parts, stepIndex + 1, parts.Length) : null;
        if (startToken.Length is 0
            || endToken.Length is 0
            || (stepIndex >= 0 && stepToken!.Length is 0))
        {
            error = ForSyntaxError;
            return true;
        }

        header = new RunScriptForHeader(variableName, startToken, endToken, stepToken, stepIndex >= 0);
        return true;
    }

    private static int IndexOfKeyword(string[] parts, string keyword, int startIndex)
    {
        for (var i = startIndex; i < parts.Length; i++)
        {
            if (string.Equals(parts[i], keyword, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string JoinSegment(string[] parts, int startIndex, int endIndex)
    {
        return endIndex <= startIndex
            ? string.Empty
            : string.Join(' ', parts, startIndex, endIndex - startIndex);
    }
}
