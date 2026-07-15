using System;

namespace CrossMacro.Core.Models;

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
