
namespace CrossMacro.Infrastructure.Services.Playback;

internal static class WindowCommandHelpers
{
    public static string StripDollar(string token) => token.StartsWith('$') ? token[1..] : token;

    public static bool IsValidVarName(string name) =>
        name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') &&
        name.AsSpan().IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_".AsSpan()) < 0;

    public static string Unquote(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
        {
            return s[1..^1]
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal)
                .Replace("\\'", "'", StringComparison.Ordinal);
        }

        return s;
    }

    public static WindowInfo? FindByTitle(IReadOnlyList<WindowInfo> windows, string substr) =>
        FindFirst(windows, w => w.Title.Contains(substr, StringComparison.OrdinalIgnoreCase));

    public static WindowInfo? FindByClass(IReadOnlyList<WindowInfo> windows, string substr) =>
        FindFirst(windows, w => w.Class.Contains(substr, StringComparison.OrdinalIgnoreCase));

    private static WindowInfo? FindFirst(IReadOnlyList<WindowInfo> windows, Func<WindowInfo, bool> predicate)
    {
        return windows.FirstOrDefault(predicate);
    }

    public static void StoreVariable(IDictionary<string, string> variables, string name, string value, int stepNumber)
    {
        if (!IsValidVarName(name))
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: invalid variable name '{name}'.");
        }

        variables[name] = value;
    }
}
