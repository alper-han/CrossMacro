
namespace CrossMacro.UI.Converters;

/// <summary>
/// Converters for EditorActionType visibility in the editor UI.
/// </summary>
public static class ActionTypeConverters
{
    /// <summary>
    /// Returns true if the action type is a mouse-related action.
    /// </summary>
    public static readonly IValueConverter IsMouseAction = new FuncValueConverter<EditorActionType, bool>(type =>
        type is EditorActionType.MouseMove
            or EditorActionType.MouseClick
            or EditorActionType.MouseDown
            or EditorActionType.MouseUp);

    /// <summary>
    /// Returns true if the action type is a click action.
    /// </summary>
    public static readonly IValueConverter IsClickAction = new FuncValueConverter<EditorActionType, bool>(type =>
        type is EditorActionType.MouseClick
            or EditorActionType.MouseDown
            or EditorActionType.MouseUp);

    /// <summary>
    /// Returns true if the action type is a keyboard action.
    /// </summary>
    public static readonly IValueConverter IsKeyAction = new FuncValueConverter<EditorActionType, bool>(type =>
        type is EditorActionType.KeyPress
            or EditorActionType.KeyDown
            or EditorActionType.KeyUp);

    /// <summary>
    /// Returns true if the action type is a scroll action.
    /// </summary>
    public static readonly IValueConverter IsScrollAction = new FuncValueConverter<EditorActionType, bool>(type =>
        type is EditorActionType.ScrollVertical
            or EditorActionType.ScrollHorizontal);

}
