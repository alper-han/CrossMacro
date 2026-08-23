namespace CrossMacro.Platform.MacOS.Services;

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

        int value = 0;
        if (type is CoreGraphics.CGEventType.KeyDown)
        {
            value = 1;
        }
        else if (type is CoreGraphics.CGEventType.FlagsChanged)
        {
            value = IsModifierPressed(code, flags) ? 1 : 0;
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
        long value,
        long timestamp,
        long timestampMicroseconds,
        out CapturedInputEvent inputEvent)
    {
        inputEvent = default;
        if (value is 0 || code is not (InputEventCode.REL_WHEEL or InputEventCode.REL_HWHEEL))
        {
            return false;
        }

        inputEvent = new CapturedInputEvent
        {
            Type = InputEventType.MouseScroll,
            Code = code,
            Value = (int)Math.Clamp(value, int.MinValue, int.MaxValue),
            Timestamp = timestamp,
            TimestampMicroseconds = timestampMicroseconds,
        };
        return true;
    }

    internal static long ResolveScrollDelta(
        bool isContinuous,
        long lineDelta,
        long pointDelta,
        long fixedPointDelta)
    {
        if (!isContinuous)
        {
            return lineDelta;
        }

        if (pointDelta is not 0)
        {
            return pointDelta;
        }

        if (fixedPointDelta is not 0)
        {
            return Math.Sign(fixedPointDelta);
        }

        return lineDelta;
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

    internal static CoreGraphics.CGEventTapOptions CreateObserveOnlyTapOptions()
        => CoreGraphics.CGEventTapOptions.ListenOnly;

    internal static bool ShouldIgnoreKeyboardEvent(long eventSourceUserData)
        => eventSourceUserData == InputEventMarkers.TextExpansionKeyboardEvent;

    internal static bool ShouldReenableEventTap(CoreGraphics.CGEventType type) =>
        type is CoreGraphics.CGEventType.TapDisabledByTimeout
            or CoreGraphics.CGEventType.TapDisabledByUserInput;

    internal static long GetCurrentTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static ulong EventMask(CoreGraphics.CGEventType type) => 1UL << (int)type;

    private static bool IsModifierPressed(int code, CoreGraphics.CGEventModifiers flags)
    {
        if (code is InputEventCode.KEY_LEFTSHIFT or InputEventCode.KEY_RIGHTSHIFT)
        {
            return flags.HasFlag(CoreGraphics.CGEventModifiers.Shift);
        }

        if (code is InputEventCode.KEY_LEFTCTRL or InputEventCode.KEY_RIGHTCTRL)
        {
            return flags.HasFlag(CoreGraphics.CGEventModifiers.Control);
        }

        if (code is InputEventCode.KEY_LEFTALT or InputEventCode.KEY_RIGHTALT)
        {
            return flags.HasFlag(CoreGraphics.CGEventModifiers.Alternate);
        }

        if (code is InputEventCode.KEY_LEFTMETA or InputEventCode.KEY_RIGHTMETA)
        {
            return flags.HasFlag(CoreGraphics.CGEventModifiers.Command);
        }

        if (code == InputEventCode.KEY_CAPSLOCK)
        {
            return flags.HasFlag(CoreGraphics.CGEventModifiers.AlphaShift);
        }

        return false;
    }
}
