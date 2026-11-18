namespace CrossMacro.Editing.Models;

/// <summary>Which editor actions carry mouse coordinates, and which act at the current cursor position instead.</summary>
public static class EditorActionCoordinateRules
{
    /// <summary>Whether the action type has coordinate fields (move and the three button actions).</summary>
    public static bool UsesCoordinates(EditorActionType actionType) =>
        actionType is EditorActionType.MouseMove or EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp;

    /// <summary>Whether the action is a button action that acts at the current cursor position and therefore has no coordinates.</summary>
    public static bool IsCurrentPositionMouseButton(EditorAction? action) =>
        action?.Type is EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp
        && action.UseCurrentPosition;

    /// <summary>Whether the action carries coordinates that are applied (a move, or a button action at an explicit position).</summary>
    public static bool HasPositionCoordinates(EditorAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return UsesCoordinates(action.Type) && !IsCurrentPositionMouseButton(action);
    }

    /// <summary>Clears the coordinates a current-position button action does not use.</summary>
    public static void NormalizeCurrentPositionMouseButton(EditorAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!IsCurrentPositionMouseButton(action))
        {
            return;
        }

        action.X = 0;
        action.Y = 0;
        action.IsAbsolute = false;
    }

    /// <summary>Applies <see cref="NormalizeCurrentPositionMouseButton"/> to every action.</summary>
    public static void NormalizeCurrentPositionMouseButtons(IEnumerable<EditorAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        foreach (var action in actions)
        {
            NormalizeCurrentPositionMouseButton(action);
        }
    }
}
