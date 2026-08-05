namespace CrossMacro.Platform.Windows.Services;

/// <summary>
/// Pure mapping rules for Windows low-level hook messages.
/// Native hook installation and callback lifecycle remain owned by
/// <see cref="WindowsInputCapture"/>.
/// </summary>
internal static class WindowsInputEventPolicy
{
    private const uint LowLevelKeyboardHookFlagExtended = 0x01;
    private const uint LowLevelKeyboardHookFlagLowerIntegrityInjected = 0x02;
    private const uint LowLevelKeyboardHookFlagInjected = 0x10;
    private const int WtsSessionUnlock = 0x8;
    private const int WtsSessionDesktopReady = 0xF;

    internal static bool TryMapMouseButtonOrScroll(
        uint msg,
        uint mouseData,
        out ushort evdevCode,
        out int value,
        out ushort type)
    {
        evdevCode = 0;
        value = 0;
        type = InputEventCode.EV_KEY;

        switch (msg)
        {
            case User32.WM_LBUTTONDOWN:
                evdevCode = (ushort)InputEventCode.BTN_LEFT;
                value = 1;
                return true;
            case User32.WM_LBUTTONUP:
                evdevCode = (ushort)InputEventCode.BTN_LEFT;
                return true;
            case User32.WM_RBUTTONDOWN:
                evdevCode = (ushort)InputEventCode.BTN_RIGHT;
                value = 1;
                return true;
            case User32.WM_RBUTTONUP:
                evdevCode = (ushort)InputEventCode.BTN_RIGHT;
                return true;
            case User32.WM_MBUTTONDOWN:
                evdevCode = (ushort)InputEventCode.BTN_MIDDLE;
                value = 1;
                return true;
            case User32.WM_MBUTTONUP:
                evdevCode = (ushort)InputEventCode.BTN_MIDDLE;
                return true;
            case User32.WM_MOUSEWHEEL:
                type = InputEventCode.EV_REL;
                evdevCode = InputEventCode.REL_WHEEL;
                value = (short)((mouseData >> 16) & 0xFFFF);
                return true;
            case User32.WM_XBUTTONDOWN:
            case User32.WM_XBUTTONUP:
                switch ((ushort)(mouseData >> 16))
                {
                    case User32.XBUTTON1:
                        evdevCode = (ushort)InputEventCode.BTN_SIDE;
                        break;
                    case User32.XBUTTON2:
                        evdevCode = (ushort)InputEventCode.BTN_EXTRA;
                        break;
                    default:
                        return false;
                }

                value = msg == User32.WM_XBUTTONDOWN ? 1 : 0;
                return true;
            case User32.WM_MOUSEHWHEEL:
                type = InputEventCode.EV_REL;
                evdevCode = InputEventCode.REL_HWHEEL;
                value = (short)((mouseData >> 16) & 0xFFFF);
                return true;
            default:
                return false;
        }
    }

    internal static (ushort XCode, int XValue, ushort YCode, int YValue) ResolveMouseMovement(
        bool useAbsoluteCoordinates,
        int currentX,
        int currentY,
        int previousX,
        int previousY) => useAbsoluteCoordinates
            ? (InputEventCode.ABS_X, currentX, InputEventCode.ABS_Y, currentY)
            : (
                InputEventCode.REL_X,
                (int)Math.Clamp((long)currentX - previousX, int.MinValue, int.MaxValue),
                InputEventCode.REL_Y,
                (int)Math.Clamp((long)currentY - previousY, int.MinValue, int.MaxValue));

    internal static int MapKeyboardEvent(ushort virtualKey, uint hookFlags)
    {
        int evdevCode = WindowsKeyMap.GetEvdevCode(virtualKey);
        bool isExtended = (hookFlags & LowLevelKeyboardHookFlagExtended) == LowLevelKeyboardHookFlagExtended;

        if (virtualKey is 0x12 or 0xA4 && isExtended)
        {
            return InputEventCode.KEY_RIGHTALT;
        }

        if (virtualKey is 0x11 or 0xA2 && isExtended)
        {
            return InputEventCode.KEY_RIGHTCTRL;
        }

        return virtualKey is 0x0D && isExtended ? InputEventCode.KEY_KPENTER : evdevCode;
    }

    internal static bool ShouldIgnoreKeyboardHookEvent(uint hookFlags, IntPtr extraInfo)
    {
        var isInjected = (hookFlags & (LowLevelKeyboardHookFlagInjected | LowLevelKeyboardHookFlagLowerIntegrityInjected)) != 0;
        return isInjected && extraInfo == InputEventMarkers.ToIntPtr(InputEventMarkers.TextExpansionKeyboardEvent);
    }

    internal static bool IsSessionRecoveryMessage(uint message, IntPtr wParam)
    {
        if (message != User32.WM_WTSSESSION_CHANGE)
        {
            return false;
        }

        var reason = wParam.ToInt32();
        return reason is WtsSessionUnlock or WtsSessionDesktopReady;
    }
}
