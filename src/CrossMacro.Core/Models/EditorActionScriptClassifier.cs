namespace CrossMacro.Core.Models;

/// <summary>
/// Classifies editor action types for script-aware save/validation flows.
/// </summary>
public static class EditorActionScriptClassifier
{
    public static bool IsScriptFlowControlAction(EditorActionType actionType)
    {
        return actionType is
            EditorActionType.RepeatBlockStart
            or EditorActionType.IfBlockStart
            or EditorActionType.ElseBlockStart
            or EditorActionType.WhileBlockStart
            or EditorActionType.ForBlockStart
            or EditorActionType.Break
            or EditorActionType.Continue
            or EditorActionType.BlockEnd;
    }

    public static bool IsScriptStateAction(EditorActionType actionType)
    {
        return actionType is
            EditorActionType.SetVariable
            or EditorActionType.IncrementVariable
            or EditorActionType.DecrementVariable;
    }

    public static bool IsOpaqueScriptAction(EditorActionType actionType)
    {
        return actionType == EditorActionType.RawScriptStep;
    }

    public static bool IsRuntimeEventAction(EditorActionType actionType)
    {
        return actionType is
            EditorActionType.MouseMove
            or EditorActionType.MouseClick
            or EditorActionType.MouseDown
            or EditorActionType.MouseUp
            or EditorActionType.KeyPress
            or EditorActionType.KeyDown
            or EditorActionType.KeyUp
            or EditorActionType.ScrollVertical
            or EditorActionType.ScrollHorizontal
            or EditorActionType.TextInput;
    }

    public static bool IsScriptBlockStartAction(EditorActionType actionType)
    {
        return actionType is
            EditorActionType.RepeatBlockStart
            or EditorActionType.IfBlockStart
            or EditorActionType.ElseBlockStart
            or EditorActionType.WhileBlockStart
            or EditorActionType.ForBlockStart;
    }

    public static bool IsLoopBlockStartAction(EditorActionType actionType)
    {
        return actionType is
            EditorActionType.RepeatBlockStart
            or EditorActionType.WhileBlockStart
            or EditorActionType.ForBlockStart;
    }

    public static bool IsLoopControlAction(EditorActionType actionType)
    {
        return actionType is EditorActionType.Break or EditorActionType.Continue;
    }
}
