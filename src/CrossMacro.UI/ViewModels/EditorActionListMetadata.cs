
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
                or EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal or EditorActionType.ImageClick => EditorActionVisualKind.PointerInput,
            EditorActionType.KeyPress or EditorActionType.KeyDown or EditorActionType.KeyUp => EditorActionVisualKind.Keyboard,
            EditorActionType.TextInput or EditorActionType.ClipboardSet => EditorActionVisualKind.Text,
            EditorActionType.Delay => EditorActionVisualKind.Timing,
            EditorActionType.SetVariable or EditorActionType.ClipboardGet or EditorActionType.ShellCommand
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
        if (isNoise)
        {
            return false;
        }

        return action.Type switch
        {
            EditorActionType.MouseMove => false,
            EditorActionType.Delay when !action.UseRandomDelay && action.DelayMicroseconds is 0 => false,
            EditorActionType.MouseClick => true,
            EditorActionType.MouseDown => true,
            EditorActionType.MouseUp => true,
            EditorActionType.KeyPress => true,
            EditorActionType.KeyDown => true,
            EditorActionType.KeyUp => true,
            EditorActionType.Delay => true,
            EditorActionType.ScrollVertical => true,
            EditorActionType.ScrollHorizontal => true,
            EditorActionType.TextInput => true,
            EditorActionType.SetVariable => true,
            EditorActionType.IncrementVariable => true,
            EditorActionType.DecrementVariable => true,
            EditorActionType.MultiplyVariable => true,
            EditorActionType.DivideVariable => true,
            EditorActionType.RepeatBlockStart => true,
            EditorActionType.IfBlockStart => true,
            EditorActionType.ElseBlockStart => true,
            EditorActionType.WhileBlockStart => true,
            EditorActionType.ForBlockStart => true,
            EditorActionType.BlockEnd => true,
            EditorActionType.Break => true,
            EditorActionType.Continue => true,
            EditorActionType.PixelColor => true,
            EditorActionType.WaitColor => true,
            EditorActionType.PixelSearch => true,
            EditorActionType.ImageSearch => true,
            EditorActionType.ImageClick => true,
            EditorActionType.WaitImage => true,
            EditorActionType.ClipboardGet => true,
            EditorActionType.MousePosition => true,
            EditorActionType.ClipboardSet => true,
            EditorActionType.ShellCommand => true,
            EditorActionType.Screenshot => true,
            EditorActionType.WindowCommand => true,
            EditorActionType.RawScriptStep => true,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action.Type, message: null),
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
        return action is { Type: EditorActionType.Delay, UseRandomDelay: false, DelayMicroseconds: > 0 and < 10_000 };
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
