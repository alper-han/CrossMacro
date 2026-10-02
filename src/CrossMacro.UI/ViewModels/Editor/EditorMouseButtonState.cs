namespace CrossMacro.UI.ViewModels.Editor;

internal struct EditorMouseButtonState(byte pressedButtons)
{
    private const byte LeftMask = 1 << 0;
    private const byte RightMask = 1 << 1;
    private const byte MiddleMask = 1 << 2;
    private const byte Side1Mask = 1 << 3;
    private const byte Side2Mask = 1 << 4;
    private const byte AllButtonsMask = LeftMask | RightMask | MiddleMask | Side1Mask | Side2Mask;

    public byte PressedButtons { readonly get; private set; } = pressedButtons;

    public readonly bool IsAnyButtonPressed => PressedButtons is not 0;

    public void Update(EditorAction action)
    {
        if (action.Type is EditorActionType.RawScriptStep)
        {
            PressedButtons = AllButtonsMask;
            return;
        }

        var mask = GetButtonMask(action.Button);
        if (mask is 0)
        {
            return;
        }

        if (action.Type is EditorActionType.MouseDown)
        {
            PressedButtons |= mask;
        }
        else if (action.Type is EditorActionType.MouseUp)
        {
            PressedButtons &= (byte)~mask;
        }
    }

    private static byte GetButtonMask(MacroMouseButton button)
    {
        return button switch
        {
            MacroMouseButton.Left => LeftMask,
            MacroMouseButton.Right => RightMask,
            MacroMouseButton.Middle => MiddleMask,
            MacroMouseButton.Side1 => Side1Mask,
            MacroMouseButton.Side2 => Side2Mask,
            MacroMouseButton.None => LeftMask,
            MacroMouseButton.ScrollUp => 0,
            MacroMouseButton.ScrollDown => 0,
            MacroMouseButton.ScrollLeft => 0,
            MacroMouseButton.ScrollRight => 0,
            _ => 0,
        };
    }
}
