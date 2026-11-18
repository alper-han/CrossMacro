using System.Text;

namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>Quote handling shared by the script reader, editor reader and runtime executors.</summary>
internal static class ScriptQuoting
{
    /// <summary>
    /// Removes one pair of matching surrounding quotes without touching the content.
    /// Used for values that were already resolved and carry no escape sequences.
    /// </summary>
    public static string StripQuotes(string input) =>
        IsQuoted(input) ? input[1..^1] : input;

    /// <summary>
    /// Trims <paramref name="value"/> and, when it is wrapped in matching quotes, removes them and resolves the
    /// <c>\"</c>, <c>\'</c> and <c>\\</c> escapes in one pass. Unquoted text is returned trimmed and unchanged.
    /// </summary>
    public static string Unquote(string value)
    {
        var trimmed = value.Trim();
        if (!IsQuoted(trimmed))
        {
            return trimmed;
        }

        var builder = new StringBuilder(trimmed.Length - 2);
        var last = trimmed.Length - 1;
        for (var index = 1; index < last; index++)
        {
            var current = trimmed[index];
            if (current == '\\' && index + 1 < last && trimmed[index + 1] is '"' or '\'' or '\\')
            {
                current = trimmed[++index];
            }

            _ = builder.Append(current);
        }

        return builder.ToString();
    }

    private static bool IsQuoted(string value) =>
        value.Length >= 2 && value[0] is '"' or '\'' && value[^1] == value[0];
}
