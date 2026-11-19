using System.Text.RegularExpressions;

namespace CrossMacro.Core.Services.Automation.Triggers;

/// <summary>Matches focused-window values using the rule semantics shared by shortcuts and triggers.</summary>
public static class WindowRuleMatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

    public static bool IsValid(TriggerField field, TriggerMatchMode matchMode, string value)
    {
        if ((field is not (TriggerField.WindowClass or TriggerField.WindowTitle or TriggerField.ProcessName))
            || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (matchMode is TriggerMatchMode.Equals or TriggerMatchMode.Contains)
        {
            return true;
        }

        return matchMode is TriggerMatchMode.Regex && TryRegexIsMatch(string.Empty, value, out _);
    }

    public static bool IsMatch(TriggerMatchMode matchMode, string value, string? actual)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrEmpty(actual))
        {
            return false;
        }

        if (matchMode is TriggerMatchMode.Regex)
        {
            return TryRegexIsMatch(actual, value, out var matched) && matched;
        }

        if (matchMode is TriggerMatchMode.Equals)
        {
            return string.Equals(actual, value, StringComparison.Ordinal);
        }

        return matchMode is TriggerMatchMode.Contains && actual.Contains(value, StringComparison.Ordinal);
    }

    public static bool IsMatch(TriggerField field, TriggerMatchMode matchMode, string value, string? windowTitle, string? windowClass, string? processName)
    {
        if (field is TriggerField.WindowTitle)
        {
            return IsMatch(matchMode, value, windowTitle);
        }

        if (field is TriggerField.WindowClass)
        {
            return IsMatch(matchMode, value, windowClass);
        }

        return field is TriggerField.ProcessName && IsMatch(matchMode, value, processName);
    }

    /// <summary>Returns false when the pattern is unsupported or exceeds the match timeout.</summary>
    private static bool TryRegexIsMatch(string input, string pattern, out bool matched)
    {
        try
        {
            matched = Regex.IsMatch(input, pattern, RegexOptions.NonBacktracking, RegexTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            matched = false;
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            matched = false;
            return false;
        }
        catch (NotSupportedException)
        {
            matched = false;
            return false;
        }
    }
}
