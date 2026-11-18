namespace CrossMacro.Platform.Linux.Native.X11;

/// <summary>
/// Translates between X11 and Linux input numbering: keycodes are offset by a constant, and a few mouse buttons
/// are numbered differently.
/// </summary>
internal static class X11InputCodes
{
    /// <summary>An X11 keycode is the Linux keycode plus this value.</summary>
    internal const int KeycodeOffset = 8;

    private static readonly (int X11, int Linux)[] Buttons =
    [
        (1, UInputNative.BTN_LEFT),
        (2, UInputNative.BTN_MIDDLE),
        (3, UInputNative.BTN_RIGHT),
        (8, UInputNative.BTN_SIDE),
        (9, UInputNative.BTN_EXTRA),
    ];

    internal static uint ToX11Keycode(int linuxKeyCode) => (uint)(linuxKeyCode + KeycodeOffset);

    internal static int ToLinuxKeyCode(int x11Keycode) => x11Keycode - KeycodeOffset;

    /// <summary>Returns the Linux button code for an X11 button, or the X11 number itself when it has no Linux equivalent.</summary>
    internal static int ToLinuxButton(int x11Button)
    {
        foreach (var (x11, linux) in Buttons)
        {
            if (x11 == x11Button)
            {
                return linux;
            }
        }

        return x11Button;
    }

    /// <summary>Returns the X11 button that scrolls by <paramref name="delta"/> along the chosen axis.</summary>
    internal static uint ScrollButton(int delta, bool isHorizontal)
    {
        if (isHorizontal)
        {
            return (uint)(delta > 0 ? XInput2Consts.X11_SCROLL_RIGHT : XInput2Consts.X11_SCROLL_LEFT);
        }

        return (uint)(delta > 0 ? XInput2Consts.X11_SCROLL_UP : XInput2Consts.X11_SCROLL_DOWN);
    }

    internal static bool TryToX11Button(int linuxButton, out uint x11Button)
    {
        foreach (var (x11, linux) in Buttons)
        {
            if (linux == linuxButton)
            {
                x11Button = (uint)x11;
                return true;
            }
        }

        x11Button = 0;
        return false;
    }
}
