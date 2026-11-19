
namespace CrossMacro.UI.ViewModels.Editor;

internal static class EditorActionListMetadata
{
    public static EditorActionVisualKind GetVisualKind(EditorAction action, bool isNoise)
    {
        return action.Type switch
        {
            EditorActionType.Delay when isNoise => EditorActionVisualKind.Noise,
            EditorActionType.MouseMove => EditorActionVisualKind.Movement,
            EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp
                or EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal or EditorActionType.ImageClick => EditorActionVisualKind.PointerInput,
            EditorActionType.KeyPress or EditorActionType.KeyDown or EditorActionType.KeyUp => EditorActionVisualKind.Keyboard,
            EditorActionType.TextInput or EditorActionType.ClipboardSet => EditorActionVisualKind.Text,
            EditorActionType.Delay => EditorActionVisualKind.Timing,
            EditorActionType.SetVariable or EditorActionType.ClipboardGet or EditorActionType.CopySelectionToVariable or EditorActionType.ShellCommand
                or EditorActionType.MousePosition
                or EditorActionType.IncrementVariable or EditorActionType.DecrementVariable
                or EditorActionType.MultiplyVariable or EditorActionType.DivideVariable => EditorActionVisualKind.Variable,
            EditorActionType.PixelColor or EditorActionType.WaitColor or EditorActionType.PixelSearch
                or EditorActionType.Screenshot or EditorActionType.RawScriptStep or EditorActionType.ImageSearch
                or EditorActionType.WaitImage or EditorActionType.WindowCommand => EditorActionVisualKind.Raw,
            EditorActionType.RepeatBlockStart or EditorActionType.IfBlockStart or EditorActionType.ElseBlockStart
                or EditorActionType.WhileBlockStart or EditorActionType.ForBlockStart or EditorActionType.BlockEnd
                or EditorActionType.Break or EditorActionType.Continue => EditorActionVisualKind.ControlFlow,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action.Type, message: null),
        };
    }

    public static bool IsImportant(EditorAction action, bool isNoise)
    {
        return !isNoise
            && action.Type is not EditorActionType.MouseMove
            && action is not { Type: EditorActionType.Delay, UseRandomDelay: false, DelayMicroseconds: 0 };
    }

    public static bool IsCleanupEligible(EditorAction action, bool isNoise)
    {
        return isNoise && (action.Type is EditorActionType.MouseMove or EditorActionType.Delay);
    }

    public static bool IsHidden(
        EditorAction action,
        bool hideMouseMoves,
        bool hideShortWaits,
        bool isDefinitelyIdle)
    {
        return isDefinitelyIdle
            && ((hideMouseMoves && action.Type is EditorActionType.MouseMove)
                || (hideShortWaits && IsShortWait(action)));
    }

    public static bool HasHiddenActions(
        IReadOnlyList<EditorAction> actions,
        bool hideMouseMoves,
        bool hideShortWaits,
        EditorMouseButtonAnalysis buttonAnalysis)
    {
        for (var index = 0; index < actions.Count; index++)
        {
            if (IsHidden(actions[index], hideMouseMoves, hideShortWaits, buttonAnalysis.IsDefinitelyIdle(index)))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsLowImportance(EditorAction action, bool isInsideDrag)
    {
        return (!isInsideDrag && action.Type is EditorActionType.MouseMove) || IsShortWait(action);
    }

    public static bool IsShortWait(EditorAction action)
    {
        return action is { Type: EditorActionType.Delay, UseRandomDelay: false, DelayMicroseconds: > 0 and < 10_000 };
    }

}
