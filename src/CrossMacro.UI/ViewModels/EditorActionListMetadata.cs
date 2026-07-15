using CrossMacro.Core.Models;

namespace CrossMacro.UI.ViewModels;

internal static class EditorActionListMetadata
{
    public static EditorActionVisualKind GetVisualKind(EditorAction action, bool isNoise)
    {
        return action.Type switch
        {
            EditorActionType.Delay when isNoise => EditorActionVisualKind.Noise,
            EditorActionType.MouseMove => EditorActionVisualKind.Movement,
            EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp
                or EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal => EditorActionVisualKind.Pointer,
            EditorActionType.KeyPress or EditorActionType.KeyDown or EditorActionType.KeyUp => EditorActionVisualKind.Keyboard,
            EditorActionType.TextInput or EditorActionType.ClipboardSet => EditorActionVisualKind.Text,
            EditorActionType.Delay => EditorActionVisualKind.Timing,
            EditorActionType.SetVariable or EditorActionType.ClipboardGet or EditorActionType.ShellCommand
                or EditorActionType.IncrementVariable or EditorActionType.DecrementVariable => EditorActionVisualKind.Variable,
            EditorActionType.PixelColor or EditorActionType.WaitColor or EditorActionType.PixelSearch
                or EditorActionType.Screenshot or EditorActionType.RawScriptStep => EditorActionVisualKind.Raw,
            EditorActionType.RepeatBlockStart or EditorActionType.IfBlockStart or EditorActionType.ElseBlockStart
                or EditorActionType.WhileBlockStart or EditorActionType.ForBlockStart or EditorActionType.BlockEnd
                or EditorActionType.Break or EditorActionType.Continue => EditorActionVisualKind.ControlFlow,
            _ => EditorActionVisualKind.Raw,
        };
    }

    public static bool IsImportant(EditorAction action, bool isNoise)
    {
        if (isNoise)
        {
            return false;
        }

        return action.Type switch
        {
            EditorActionType.MouseMove => false,
            EditorActionType.Delay when !action.UseRandomDelay && action.DelayMs is 0 => false,
            _ => true,
        };
    }

    public static bool IsCleanupEligible(EditorAction action, bool isNoise)
    {
        return isNoise && (action.Type is EditorActionType.MouseMove or EditorActionType.Delay);
    }

    public static bool IsHidden(EditorAction action, bool hideMouseMoves, bool hideShortWaits)
    {
        return (hideMouseMoves && action.Type is EditorActionType.MouseMove)
|| (hideShortWaits && IsShortWait(action));
    }

    public static bool IsLowImportance(EditorAction action, bool isInsideDrag)
    {
        return (!isInsideDrag && action.Type is EditorActionType.MouseMove) || IsShortWait(action);
    }

    public static bool IsMovementCandidate(EditorAction action)
    {
        return action.Type is EditorActionType.MouseMove || IsShortWait(action);
    }

    public static bool IsShortWait(EditorAction action)
    {
        return action is { Type: EditorActionType.Delay, UseRandomDelay: false, DelayMs: > 0 and < 10 };
    }

    public static void UpdateDragState(EditorAction action, ref bool isDragging)
    {
        switch (action.Type)
        {
            case EditorActionType.MouseDown:
                isDragging = true;
                break;
            case EditorActionType.MouseUp:
            case EditorActionType.MouseClick:
                isDragging = false;
                break;
        }
    }
}
