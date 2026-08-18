namespace CrossMacro.Platform.MacOS.Services;

/// <summary>
/// Pure routing and state mapping rules for macOS input injection.
/// Native event creation/posting and permission lifecycle stay in the simulator.
/// </summary>
internal static class MacOSInputSimulationPolicy
{
    internal static bool TryResolveMouseButton(
        int button,
        bool pressed,
        out CoreGraphics.CGMouseButton macButton,
        out CoreGraphics.CGEventType eventType,
        out long buttonNumber)
    {
        buttonNumber = button switch
        {
            MouseButtonCode.Left => 0,
            MouseButtonCode.Right => 1,
            MouseButtonCode.Middle => 2,
            MouseButtonCode.Side1 => 3,
            MouseButtonCode.Side2 => 4,
            _ => -1,
        };

        if (buttonNumber < 0)
        {
            macButton = default;
            eventType = default;
            return false;
        }

        macButton = (CoreGraphics.CGMouseButton)buttonNumber;
        eventType = button switch
        {
            MouseButtonCode.Left => pressed ? CoreGraphics.CGEventType.LeftMouseDown : CoreGraphics.CGEventType.LeftMouseUp,
            MouseButtonCode.Right => pressed ? CoreGraphics.CGEventType.RightMouseDown : CoreGraphics.CGEventType.RightMouseUp,
            MouseButtonCode.Middle or MouseButtonCode.Side1 or MouseButtonCode.Side2 =>
                pressed ? CoreGraphics.CGEventType.OtherMouseDown : CoreGraphics.CGEventType.OtherMouseUp,
            _ => throw new UnreachableException(),
        };
        return true;
    }

    internal static (CoreGraphics.CGEventType EventType, CoreGraphics.CGMouseButton Button, long ButtonNumber)
        ResolveMouseMovement(IReadOnlySet<int> pressedButtons)
    {
        ArgumentNullException.ThrowIfNull(pressedButtons);

        int button = -1;
        if (pressedButtons.Contains(MouseButtonCode.Left))
        {
            button = MouseButtonCode.Left;
        }
        else if (pressedButtons.Contains(MouseButtonCode.Right))
        {
            button = MouseButtonCode.Right;
        }
        else if (pressedButtons.Contains(MouseButtonCode.Middle))
        {
            button = MouseButtonCode.Middle;
        }
        else if (pressedButtons.Contains(MouseButtonCode.Side1))
        {
            button = MouseButtonCode.Side1;
        }
        else if (pressedButtons.Contains(MouseButtonCode.Side2))
        {
            button = MouseButtonCode.Side2;
        }

        return button switch
        {
            MouseButtonCode.Left => (CoreGraphics.CGEventType.LeftMouseDragged, CoreGraphics.CGMouseButton.Left, 0),
            MouseButtonCode.Right => (CoreGraphics.CGEventType.RightMouseDragged, CoreGraphics.CGMouseButton.Right, 1),
            MouseButtonCode.Middle => (CoreGraphics.CGEventType.OtherMouseDragged, CoreGraphics.CGMouseButton.Center, 2),
            MouseButtonCode.Side1 => (CoreGraphics.CGEventType.OtherMouseDragged, (CoreGraphics.CGMouseButton)3, 3),
            MouseButtonCode.Side2 => (CoreGraphics.CGEventType.OtherMouseDragged, (CoreGraphics.CGMouseButton)4, 4),
            _ => (CoreGraphics.CGEventType.MouseMoved, CoreGraphics.CGMouseButton.Left, 0),
        };
    }

    internal static bool TryGetSystemDefinedKeyType(int keyCode, out int nxKeyType)
        => MacOSSystemKeyEventFactory.TryGetNxKeyType(keyCode, out nxKeyType);

    internal static MacOSKeyboardEventRoute ResolveKeyboardEventRoute(
        int keyCode,
        out int nxKeyType,
        out ushort virtualKeyCode)
    {
        if (TryGetSystemDefinedKeyType(keyCode, out nxKeyType))
        {
            virtualKeyCode = 0xFFFF;
            return MacOSKeyboardEventRoute.SystemDefined;
        }

        virtualKeyCode = KeyMap.ToMacKey(keyCode);
        return virtualKeyCode is 0xFFFF
            ? MacOSKeyboardEventRoute.Unsupported
            : MacOSKeyboardEventRoute.Keyboard;
    }

    internal static CoreGraphics.CGEventModifiers CreateKeyboardFlags(IEnumerable<int> pressedModifierKeys)
    {
        ArgumentNullException.ThrowIfNull(pressedModifierKeys);

        var flags = default(CoreGraphics.CGEventModifiers);
        foreach (var keyCode in pressedModifierKeys)
        {
            flags |= GetModifierFlag(keyCode);
        }

        return flags;
    }

    internal static CoreGraphics.CGEventModifiers GetModifierFlag(int keyCode)
        => keyCode switch
        {
            InputEventCode.KEY_LEFTSHIFT or InputEventCode.KEY_RIGHTSHIFT => CoreGraphics.CGEventModifiers.Shift,
            InputEventCode.KEY_LEFTCTRL or InputEventCode.KEY_RIGHTCTRL => CoreGraphics.CGEventModifiers.Control,
            InputEventCode.KEY_LEFTALT or InputEventCode.KEY_RIGHTALT => CoreGraphics.CGEventModifiers.Alternate,
            InputEventCode.KEY_LEFTMETA or InputEventCode.KEY_RIGHTMETA => CoreGraphics.CGEventModifiers.Command,
            InputEventCode.KEY_CAPSLOCK => CoreGraphics.CGEventModifiers.AlphaShift,
            _ => default,
        };
}
