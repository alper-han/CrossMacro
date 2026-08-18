namespace CrossMacro.Core.Services;

/// <summary>Matches focused-window values using the rule semantics shared by shortcuts and triggers.</summary>
public static class WindowRuleMatcher
{
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

        if (matchMode is not TriggerMatchMode.Regex)
        {
            return false;
        }

        try
        {
            _ = System.Text.RegularExpressions.Regex.IsMatch(
                string.Empty,
                value,
                System.Text.RegularExpressions.RegexOptions.NonBacktracking,
                TimeSpan.FromMilliseconds(200));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public static bool IsMatch(TriggerMatchMode matchMode, string value, string? actual)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrEmpty(actual))
        {
            return false;
        }

        if (matchMode is TriggerMatchMode.Regex)
        {
            try
            {
                return System.Text.RegularExpressions.Regex.IsMatch(
                    actual,
                    value,
                    System.Text.RegularExpressions.RegexOptions.NonBacktracking,
                    TimeSpan.FromMilliseconds(200));
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        return matchMode switch
        {
            TriggerMatchMode.Equals => string.Equals(actual, value, StringComparison.Ordinal),
            TriggerMatchMode.Contains => actual.Contains(value, StringComparison.Ordinal),
            TriggerMatchMode.Regex => false,
            _ => false,
        };
    }

    public static bool IsMatch(TriggerField field, TriggerMatchMode matchMode, string value, string? windowTitle, string? windowClass, string? processName)
    {
        var actual = field switch
        {
            TriggerField.WindowTitle => windowTitle,
            TriggerField.WindowClass => windowClass,
            TriggerField.ProcessName => processName,
            TriggerField.Workspace or TriggerField.None => null,
            _ => null,
        };
        return IsMatch(matchMode, value, actual);
    }
}
