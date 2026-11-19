namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>Validates shell and window command actions.</summary>
internal static class EditorCommandActionValidator
{
    internal static (bool IsValid, string? Error) ValidateShellCommand(EditorAction action)
    {
        if (string.IsNullOrWhiteSpace(action.ShellCommand))
        {
            return (false, "Shell command cannot be empty.");
        }

        if (!Enum.IsDefined(action.ShellCommandMode))
        {
            return (false, "Shell command mode is invalid.");
        }

        if ((action.ShellCommandMode is ShellCommandMode.ShellCapture or ShellCommandMode.ShellCaptureInput)
            && (!IsValidShellCaptureTarget(action.ShellExitCodeVariableName)
                || !IsValidShellCaptureTarget(action.ShellStandardOutputVariableName)
                || !IsValidShellCaptureTarget(action.ShellStandardErrorVariableName)))
        {
            return (false, "Shell capture targets must be valid variable names, or '_' to ignore a stream.");
        }

        if (action.ShellRetries is < 0 or > EditorActionValidationLimits.MaxShellRetries)
        {
            return (false, $"Shell retries must be between 0 and {EditorActionValidationLimits.MaxShellRetries.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (action.ShellBackoffMs < 0)
        {
            return (false, "Shell backoff_ms must be non-negative.");
        }

        if (action.ShellTimeoutMs < 0)
        {
            return (false, "Shell timeout_ms must be non-negative.");
        }

        return (true, null);
    }

    internal static (bool IsValid, string? Error) ValidateWindowCommand(EditorAction action)
    {
        if (!Enum.IsDefined(action.WindowCommandMode))
        {
            return (false, "Window command mode is invalid.");
        }

        var error = RunScriptWindowExecutor.Validate(EditorScriptWriter.BuildWindowStep(action));
        return error is null ? (true, null) : (false, error);
    }

    private static bool IsValidShellCaptureTarget(string target)
    {
        return target is "_" || ScriptIdentifierSyntax.IsValidVariableName(target);
    }
}
