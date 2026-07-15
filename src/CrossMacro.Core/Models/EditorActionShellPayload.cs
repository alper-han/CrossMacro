using System;

namespace CrossMacro.Core.Models;

public readonly record struct EditorActionShellPayload(
    ShellCommandMode Mode,
    string Command,
    string StandardInput,
    string ExitCodeVariableName,
    string StandardOutputVariableName,
    string StandardErrorVariableName,
    int Retries,
    int BackoffMs,
    int TimeoutMs)
{
    public static bool TryCreate(EditorAction action, out EditorActionShellPayload payload)
    {
        ArgumentNullException.ThrowIfNull(action);
        payload = new EditorActionShellPayload(
            action.ShellCommandMode,
            action.ShellCommand,
            action.ShellStandardInput,
            action.ShellExitCodeVariableName,
            action.ShellStandardOutputVariableName,
            action.ShellStandardErrorVariableName,
            action.ShellRetries,
            action.ShellBackoffMs,
            action.ShellTimeoutMs);
        return action.Type is EditorActionType.ShellCommand;
    }
}
