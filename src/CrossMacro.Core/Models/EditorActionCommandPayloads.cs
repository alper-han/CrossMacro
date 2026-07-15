using System;

namespace CrossMacro.Core.Models;

public readonly record struct EditorActionScreenshotPayload(
    string OutputPath,
    bool CopyToClipboard,
    bool UseRegion,
    string RegionX,
    string RegionY,
    string RegionWidth,
    string RegionHeight)
{
    public static bool TryCreate(EditorAction action, out EditorActionScreenshotPayload payload)
    {
        ArgumentNullException.ThrowIfNull(action);
        payload = new EditorActionScreenshotPayload(
            action.ScreenshotOutputPath,
            action.ScreenshotCopyToClipboard,
            action.ScreenshotUseRegion,
            action.ScreenshotRegionX,
            action.ScreenshotRegionY,
            action.ScreenshotRegionWidth,
            action.ScreenshotRegionHeight);
        return action.Type is EditorActionType.Screenshot;
    }
}

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

public readonly record struct EditorActionWindowPayload(
    WindowCommandMode Mode,
    string SelectorKind,
    string SelectorValue,
    string ActiveField,
    string OutputVariable,
    int TimeoutMs,
    int X,
    int Y,
    int Width,
    int Height,
    string Workspace)
{
    public static bool TryCreate(EditorAction action, out EditorActionWindowPayload payload)
    {
        ArgumentNullException.ThrowIfNull(action);
        payload = new EditorActionWindowPayload(
            action.WindowCommandMode,
            action.WindowSelectorKind,
            action.WindowSelectorValue,
            action.WindowActiveField,
            action.WindowOutputVariable,
            action.WindowTimeoutMs,
            action.WindowX,
            action.WindowY,
            action.WindowWidth,
            action.WindowHeight,
            action.WindowWorkspace);
        return action.Type is EditorActionType.WindowCommand;
    }
}
