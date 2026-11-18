namespace CrossMacro.Platform.MacOS.Services.Input;

/// <summary>
/// Pure event-tap mapping and filtering rules used by <see cref="MacOSInputCapture"/>.
/// Native run-loop and handle ownership remains in the capture adapter.
/// </summary>
internal static class MacOSInputEventPolicy
{
    internal static bool TryMapOtherMouseButton(long buttonNumber, out int button)
    {
        button = buttonNumber switch
        {
            2 => MouseButtonCode.Middle,
            3 => MouseButtonCode.Side1,
            4 => MouseButtonCode.Side2,
            _ => 0,
        };
        return button is not 0;
    }

    internal static bool TryCreateKeyboardInput(
        CoreGraphics.CGEventType type,
        ushort nativeKeyCode,
        CoreGraphics.CGEventModifiers flags,
        long timestamp,
        long timestampMicroseconds,
        out CapturedInputEvent inputEvent)
    {
        inputEvent = default;

        if (!KeyMap.TryFromMacKey(nativeKeyCode, out var code))
        {
            return false;
        }

        bool pressed = type is CoreGraphics.CGEventType.KeyDown
            || (type is CoreGraphics.CGEventType.FlagsChanged
                && (code is InputEventCode.KEY_CAPSLOCK || IsModifierPressed(code, flags)));
        int value = pressed ? 1 : 0;

        inputEvent = new CapturedInputEvent
        {
            Type = InputEventType.Key,
            Code = code,
            Value = value,
            Timestamp = timestamp,
            TimestampMicroseconds = timestampMicroseconds,
        };

        return true;
    }

    /// <summary>
    /// Caps Lock reports its lock state rather than the key state, so every FlagsChanged for it is one press
    /// that must be followed by this release.
    /// </summary>
    internal static bool TryCreateCapsLockRelease(
        CoreGraphics.CGEventType type,
        CapturedInputEvent pressed,
        out CapturedInputEvent release)
    {
        release = pressed with { Value = 0 };
        return type is CoreGraphics.CGEventType.FlagsChanged
            && pressed is { Code: InputEventCode.KEY_CAPSLOCK, Value: 1 };
    }

    internal static bool TryCreateSystemDefinedInput(
        CoreGraphics.CGEventType type,
        long subtype,
        long data1,
        long timestamp,
        long timestampMicroseconds,
        out CapturedInputEvent inputEvent)
    {
        inputEvent = default;

        if (type is not CoreGraphics.CGEventType.SystemDefined || subtype != MacOSSystemKeyMap.NxSubtypeAuxControlButtons)
        {
            return false;
        }

        int valueState = (int)((data1 >> 8) & 0xFF);
        int value;
        if (valueState == MacOSSystemKeyMap.SystemDefinedKeyDownState)
        {
            value = 1;
        }
        else if (valueState == MacOSSystemKeyMap.SystemDefinedKeyUpState)
        {
            value = 0;
        }
        else
        {
            return false;
        }

        int keyType = (int)((data1 >> 16) & 0xFFFF);
        if (!MacOSSystemKeyMap.TryGetInputEventCode(keyType, out var code))
        {
            return false;
        }

        inputEvent = new CapturedInputEvent
        {
            Type = InputEventType.Key,
            Code = code,
            Value = value,
            Timestamp = timestamp,
            TimestampMicroseconds = timestampMicroseconds,
        };

        return true;
    }

    internal static bool TryCreateScrollInput(
        ushort code,
        int notches,
        long timestamp,
        long timestampMicroseconds,
        out CapturedInputEvent inputEvent)
    {
        inputEvent = default;
        if (notches is 0 || code is not (InputEventCode.REL_WHEEL or InputEventCode.REL_HWHEEL))
        {
            return false;
        }

        inputEvent = new CapturedInputEvent
        {
            Type = InputEventType.MouseScroll,
            Code = code,
            Value = notches,
            Timestamp = timestamp,
            TimestampMicroseconds = timestampMicroseconds,
        };
        return true;
    }

    internal static ulong CreateHidEventMask(bool useSessionSystemDefinedTap)
    {
        var mask =
            EventMask(CoreGraphics.CGEventType.KeyDown) |
            EventMask(CoreGraphics.CGEventType.KeyUp) |
            EventMask(CoreGraphics.CGEventType.FlagsChanged) |
            EventMask(CoreGraphics.CGEventType.LeftMouseDown) |
            EventMask(CoreGraphics.CGEventType.LeftMouseUp) |
            EventMask(CoreGraphics.CGEventType.RightMouseDown) |
            EventMask(CoreGraphics.CGEventType.RightMouseUp) |
            EventMask(CoreGraphics.CGEventType.OtherMouseDown) |
            EventMask(CoreGraphics.CGEventType.OtherMouseUp) |
            EventMask(CoreGraphics.CGEventType.MouseMoved) |
            EventMask(CoreGraphics.CGEventType.LeftMouseDragged) |
            EventMask(CoreGraphics.CGEventType.RightMouseDragged) |
            EventMask(CoreGraphics.CGEventType.OtherMouseDragged) |
            EventMask(CoreGraphics.CGEventType.ScrollWheel);

        if (!useSessionSystemDefinedTap)
        {
            mask |= EventMask(CoreGraphics.CGEventType.SystemDefined);
        }

        return mask;
    }

    internal static ulong CreateSystemDefinedEventMask() => EventMask(CoreGraphics.CGEventType.SystemDefined);

    internal static bool ShouldIgnoreKeyboardEvent(long eventSourceUserData)
        => eventSourceUserData == InputEventMarkers.TextExpansionKeyboardEvent;

    internal static bool ShouldReenableEventTap(CoreGraphics.CGEventType type) =>
        type is CoreGraphics.CGEventType.TapDisabledByTimeout
            or CoreGraphics.CGEventType.TapDisabledByUserInput;

    /// <summary>
    /// CGEventGetTimestamp reports nanoseconds; a zero timestamp means the event carries none and
    /// the capture-time fallback applies.
    /// </summary>
    internal static long ResolveEventTimestampMicroseconds(
        ulong eventTimestampNanoseconds,
        long fallbackTimestampMicroseconds)
        => eventTimestampNanoseconds is 0
            ? fallbackTimestampMicroseconds
            : checked((long)(eventTimestampNanoseconds / 1_000UL));

    private static ulong EventMask(CoreGraphics.CGEventType type) => 1UL << (int)type;

    private static bool IsModifierPressed(int code, CoreGraphics.CGEventModifiers flags) => code switch
    {
        InputEventCode.KEY_LEFTSHIFT => IsSideDown(
            flags,
            CoreGraphics.CGEventModifiers.Shift,
            CoreGraphics.CGEventModifiers.DeviceLeftShift,
            CoreGraphics.CGEventModifiers.DeviceRightShift),
        InputEventCode.KEY_RIGHTSHIFT => IsSideDown(
            flags,
            CoreGraphics.CGEventModifiers.Shift,
            CoreGraphics.CGEventModifiers.DeviceRightShift,
            CoreGraphics.CGEventModifiers.DeviceLeftShift),
        InputEventCode.KEY_LEFTCTRL => IsSideDown(
            flags,
            CoreGraphics.CGEventModifiers.Control,
            CoreGraphics.CGEventModifiers.DeviceLeftControl,
            CoreGraphics.CGEventModifiers.DeviceRightControl),
        InputEventCode.KEY_RIGHTCTRL => IsSideDown(
            flags,
            CoreGraphics.CGEventModifiers.Control,
            CoreGraphics.CGEventModifiers.DeviceRightControl,
            CoreGraphics.CGEventModifiers.DeviceLeftControl),
        InputEventCode.KEY_LEFTALT => IsSideDown(
            flags,
            CoreGraphics.CGEventModifiers.Alternate,
            CoreGraphics.CGEventModifiers.DeviceLeftAlternate,
            CoreGraphics.CGEventModifiers.DeviceRightAlternate),
        InputEventCode.KEY_RIGHTALT => IsSideDown(
            flags,
            CoreGraphics.CGEventModifiers.Alternate,
            CoreGraphics.CGEventModifiers.DeviceRightAlternate,
            CoreGraphics.CGEventModifiers.DeviceLeftAlternate),
        InputEventCode.KEY_LEFTMETA => IsSideDown(
            flags,
            CoreGraphics.CGEventModifiers.Command,
            CoreGraphics.CGEventModifiers.DeviceLeftCommand,
            CoreGraphics.CGEventModifiers.DeviceRightCommand),
        InputEventCode.KEY_RIGHTMETA => IsSideDown(
            flags,
            CoreGraphics.CGEventModifiers.Command,
            CoreGraphics.CGEventModifiers.DeviceRightCommand,
            CoreGraphics.CGEventModifiers.DeviceLeftCommand),
        _ => false,
    };

    // A side is down when its own device bit is set. Events that carry no device bits for the modifier
    // (synthetic or remote input) can only report the combined flag, so that flag decides.
    private static bool IsSideDown(
        CoreGraphics.CGEventModifiers flags,
        CoreGraphics.CGEventModifiers combined,
        CoreGraphics.CGEventModifiers ownSide,
        CoreGraphics.CGEventModifiers otherSide)
    {
        if ((flags & combined) is CoreGraphics.CGEventModifiers.None)
        {
            return false;
        }

        return (flags & (ownSide | otherSide)) is CoreGraphics.CGEventModifiers.None
            || (flags & ownSide) is not CoreGraphics.CGEventModifiers.None;
    }
}
