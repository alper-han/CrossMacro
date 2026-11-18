namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSInputEventPolicyTests
{
    private const ushort LeftShiftKey = 0x38;
    private const ushort RightShiftKey = 0x3C;
    private const ushort LeftControlKey = 0x3B;
    private const ushort RightControlKey = 0x3E;
    private const ushort LeftOptionKey = 0x3A;
    private const ushort RightOptionKey = 0x3D;
    private const ushort LeftCommandKey = 0x37;
    private const ushort RightCommandKey = 0x36;
    private const ushort CapsLockKey = 0x39;

    private const CoreGraphics.CGEventModifiers Shift = CoreGraphics.CGEventModifiers.Shift;
    private const CoreGraphics.CGEventModifiers Control = CoreGraphics.CGEventModifiers.Control;
    private const CoreGraphics.CGEventModifiers Alternate = CoreGraphics.CGEventModifiers.Alternate;
    private const CoreGraphics.CGEventModifiers Command = CoreGraphics.CGEventModifiers.Command;

    /// <summary>Native key, expected evdev code, combined flag, own device bit, the other side's device bit.</summary>
    public static TheoryData<ushort, int, ulong, ulong, ulong> ModifierSides => new()
    {
        { LeftShiftKey, InputEventCode.KEY_LEFTSHIFT, (ulong)Shift, (ulong)CoreGraphics.CGEventModifiers.DeviceLeftShift, (ulong)CoreGraphics.CGEventModifiers.DeviceRightShift },
        { RightShiftKey, InputEventCode.KEY_RIGHTSHIFT, (ulong)Shift, (ulong)CoreGraphics.CGEventModifiers.DeviceRightShift, (ulong)CoreGraphics.CGEventModifiers.DeviceLeftShift },
        { LeftControlKey, InputEventCode.KEY_LEFTCTRL, (ulong)Control, (ulong)CoreGraphics.CGEventModifiers.DeviceLeftControl, (ulong)CoreGraphics.CGEventModifiers.DeviceRightControl },
        { RightControlKey, InputEventCode.KEY_RIGHTCTRL, (ulong)Control, (ulong)CoreGraphics.CGEventModifiers.DeviceRightControl, (ulong)CoreGraphics.CGEventModifiers.DeviceLeftControl },
        { LeftOptionKey, InputEventCode.KEY_LEFTALT, (ulong)Alternate, (ulong)CoreGraphics.CGEventModifiers.DeviceLeftAlternate, (ulong)CoreGraphics.CGEventModifiers.DeviceRightAlternate },
        { RightOptionKey, InputEventCode.KEY_RIGHTALT, (ulong)Alternate, (ulong)CoreGraphics.CGEventModifiers.DeviceRightAlternate, (ulong)CoreGraphics.CGEventModifiers.DeviceLeftAlternate },
        { LeftCommandKey, InputEventCode.KEY_LEFTMETA, (ulong)Command, (ulong)CoreGraphics.CGEventModifiers.DeviceLeftCommand, (ulong)CoreGraphics.CGEventModifiers.DeviceRightCommand },
        { RightCommandKey, InputEventCode.KEY_RIGHTMETA, (ulong)Command, (ulong)CoreGraphics.CGEventModifiers.DeviceRightCommand, (ulong)CoreGraphics.CGEventModifiers.DeviceLeftCommand },
    };

    [Theory]
    [MemberData(nameof(ModifierSides))]
    public void FlagsChanged_WhenOnlyThisSideIsHeld_RecordsPress(
        ushort nativeKey,
        int expectedCode,
        ulong combined,
        ulong ownSide,
        ulong otherSide)
    {
        var inputEvent = CreateFlagsChanged(nativeKey, combined | ownSide);

        Assert.Equal(expectedCode, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
        Assert.NotEqual(0UL, otherSide);
    }

    [Theory]
    [MemberData(nameof(ModifierSides))]
    public void FlagsChanged_WhenThisSideIsReleasedWhileTheOtherSideIsHeld_RecordsRelease(
        ushort nativeKey,
        int expectedCode,
        ulong combined,
        ulong ownSide,
        ulong otherSide)
    {
        var inputEvent = CreateFlagsChanged(nativeKey, combined | otherSide);

        Assert.Equal(expectedCode, inputEvent.Code);
        Assert.Equal(0, inputEvent.Value);
        Assert.NotEqual(0UL, ownSide);
    }

    [Theory]
    [MemberData(nameof(ModifierSides))]
    public void FlagsChanged_WhenBothSidesAreHeld_RecordsPressForTheKeyThatChanged(
        ushort nativeKey,
        int expectedCode,
        ulong combined,
        ulong ownSide,
        ulong otherSide)
    {
        var inputEvent = CreateFlagsChanged(nativeKey, combined | ownSide | otherSide);

        Assert.Equal(expectedCode, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
    }

    [Theory]
    [MemberData(nameof(ModifierSides))]
    public void FlagsChanged_WhenNoSideIsHeldAnymore_RecordsRelease(
        ushort nativeKey,
        int expectedCode,
        ulong combined,
        ulong ownSide,
        ulong otherSide)
    {
        var inputEvent = CreateFlagsChanged(nativeKey, 0);

        Assert.Equal(expectedCode, inputEvent.Code);
        Assert.Equal(0, inputEvent.Value);
        Assert.NotEqual(0UL, combined | ownSide | otherSide);
    }

    [Theory]
    [InlineData(LeftShiftKey, (ulong)Shift)]
    [InlineData(RightControlKey, (ulong)Control)]
    [InlineData(LeftOptionKey, (ulong)Alternate)]
    [InlineData(RightCommandKey, (ulong)Command)]
    public void FlagsChanged_WhenEventCarriesNoDeviceSideBits_FallsBackToTheCombinedFlag(
        ushort nativeKey,
        ulong combined)
    {
        Assert.Equal(1, CreateFlagsChanged(nativeKey, combined).Value);
        Assert.Equal(0, CreateFlagsChanged(nativeKey, 0).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlagsChanged_ForCapsLock_AlwaysRecordsAPressRegardlessOfLockState(bool lockIsOn)
    {
        ulong flags = lockIsOn ? (ulong)CoreGraphics.CGEventModifiers.AlphaShift : 0;

        var inputEvent = CreateFlagsChanged(CapsLockKey, flags);

        Assert.Equal(InputEventCode.KEY_CAPSLOCK, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
    }

    [Fact]
    public void TryCreateCapsLockRelease_ForCapsLockPress_CompletesThePair()
    {
        var press = CreateFlagsChanged(CapsLockKey, (ulong)CoreGraphics.CGEventModifiers.AlphaShift);

        bool needsRelease = MacOSInputEventPolicy.TryCreateCapsLockRelease(
            CoreGraphics.CGEventType.FlagsChanged,
            press,
            out var release);

        Assert.True(needsRelease);
        Assert.Equal(InputEventCode.KEY_CAPSLOCK, release.Code);
        Assert.Equal(0, release.Value);
        Assert.Equal(press.Timestamp, release.Timestamp);
        Assert.Equal(press.TimestampMicroseconds, release.TimestampMicroseconds);
    }

    [Fact]
    public void TryCreateCapsLockRelease_ForOtherModifiers_NeedsNoRelease()
    {
        var shift = CreateFlagsChanged(
            LeftShiftKey,
            (ulong)(Shift | CoreGraphics.CGEventModifiers.DeviceLeftShift));

        Assert.False(MacOSInputEventPolicy.TryCreateCapsLockRelease(CoreGraphics.CGEventType.FlagsChanged, shift, out _));
    }

    [Fact]
    public void KeyDown_IsNotAffectedByModifierFlags()
    {
        bool created = MacOSInputEventPolicy.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            LeftShiftKey,
            CoreGraphics.CGEventModifiers.None,
            timestamp: 1,
            timestampMicroseconds: 2,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(1, inputEvent.Value);
    }

    [Fact]
    public void ShouldIgnoreKeyboardEvent_RecognizesOnlyCrossMacroMarker()
    {
        Assert.True(MacOSInputEventPolicy.ShouldIgnoreKeyboardEvent(InputEventMarkers.TextExpansionKeyboardEvent));
        Assert.False(MacOSInputEventPolicy.ShouldIgnoreKeyboardEvent(0));
        Assert.False(MacOSInputEventPolicy.ShouldIgnoreKeyboardEvent(123));
    }

    [Theory]
    [InlineData((uint)CoreGraphics.CGEventType.TapDisabledByTimeout, true)]
    [InlineData((uint)CoreGraphics.CGEventType.TapDisabledByUserInput, true)]
    [InlineData((uint)CoreGraphics.CGEventType.MouseMoved, false)]
    public void ShouldReenableEventTap_HandlesBothNativeDisableEvents(uint eventType, bool expected)
    {
        Assert.Equal(expected, MacOSInputEventPolicy.ShouldReenableEventTap((CoreGraphics.CGEventType)eventType));
    }

    [Theory]
    [InlineData(12_345_678UL, 12_345L)]
    [InlineData(1_000UL, 1L)]
    public void ResolveEventTimestampMicroseconds_UsesNativeQuartzNanoseconds(
        ulong nativeNanoseconds,
        long expectedMicroseconds)
    {
        Assert.Equal(
            expectedMicroseconds,
            MacOSInputEventPolicy.ResolveEventTimestampMicroseconds(nativeNanoseconds, fallbackTimestampMicroseconds: 999));
    }

    [Fact]
    public void ResolveEventTimestampMicroseconds_WhenNativeTimestampIsZero_UsesFallback()
    {
        Assert.Equal(999, MacOSInputEventPolicy.ResolveEventTimestampMicroseconds(0, fallbackTimestampMicroseconds: 999));
    }

    [Fact]
    public void TryCreateKeyboardInput_WhenNativeKeyIsUnknown_ReturnsNoMatchWithoutCodeZeroEvent()
    {
        bool created = MacOSInputEventPolicy.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            0xFFFF,
            default,
            timestamp: 123,
            timestampMicroseconds: 0,
            out _);

        Assert.False(created);
    }

    [Fact]
    public void TryCreateKeyboardInput_WhenNativeKeyIsKnown_CreatesKeyEventWithBothTimestamps()
    {
        bool created = MacOSInputEventPolicy.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            0x00,
            default,
            timestamp: 123,
            timestampMicroseconds: 123_456,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventType.Key, inputEvent.Type);
        Assert.Equal(InputEventCode.KEY_A, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
        Assert.Equal(123, inputEvent.Timestamp);
        Assert.Equal(123_456, inputEvent.TimestampMicroseconds);
    }

    [Fact]
    public void TryCreateKeyboardInput_WhenOrdinaryFunctionKeyIsKnown_RemainsKeyMapBacked()
    {
        bool created = MacOSInputEventPolicy.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            0x65,
            default,
            timestamp: 123,
            timestampMicroseconds: 0,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventCode.KEY_F9, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
    }

    [Theory]
    [InlineData(0, InputEventCode.KEY_VOLUMEUP)]
    [InlineData(1, InputEventCode.KEY_VOLUMEDOWN)]
    [InlineData(2, InputEventCode.KEY_BRIGHTNESSUP)]
    [InlineData(3, InputEventCode.KEY_BRIGHTNESSDOWN)]
    [InlineData(7, InputEventCode.KEY_MUTE)]
    [InlineData(16, InputEventCode.KEY_PLAYPAUSE)]
    [InlineData(17, InputEventCode.KEY_NEXTSONG)]
    [InlineData(18, InputEventCode.KEY_PREVIOUSSONG)]
    [InlineData(19, InputEventCode.KEY_FASTFORWARD)]
    [InlineData(20, InputEventCode.KEY_REWIND)]
    public void TryCreateSystemDefinedInput_WhenSupportedMediaKeyIsPressed_CreatesKeyDownEvent(int keyType, int expectedCode)
    {
        bool created = MacOSInputEventPolicy.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(keyType, 0x0A),
            timestamp: 123,
            timestampMicroseconds: 123_456,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventType.Key, inputEvent.Type);
        Assert.Equal(expectedCode, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
        Assert.Equal(123, inputEvent.Timestamp);
        Assert.Equal(123_456, inputEvent.TimestampMicroseconds);
    }

    [Theory]
    [InlineData(0, InputEventCode.KEY_VOLUMEUP)]
    [InlineData(1, InputEventCode.KEY_VOLUMEDOWN)]
    [InlineData(2, InputEventCode.KEY_BRIGHTNESSUP)]
    [InlineData(3, InputEventCode.KEY_BRIGHTNESSDOWN)]
    [InlineData(7, InputEventCode.KEY_MUTE)]
    [InlineData(16, InputEventCode.KEY_PLAYPAUSE)]
    [InlineData(17, InputEventCode.KEY_NEXTSONG)]
    [InlineData(18, InputEventCode.KEY_PREVIOUSSONG)]
    [InlineData(19, InputEventCode.KEY_FASTFORWARD)]
    [InlineData(20, InputEventCode.KEY_REWIND)]
    public void TryCreateSystemDefinedInput_WhenSupportedMediaKeyIsReleased_CreatesKeyUpEvent(int keyType, int expectedCode)
    {
        bool created = MacOSInputEventPolicy.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(keyType, 0x0B),
            timestamp: 456,
            timestampMicroseconds: 0,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventType.Key, inputEvent.Type);
        Assert.Equal(expectedCode, inputEvent.Code);
        Assert.Equal(0, inputEvent.Value);
        Assert.Equal(456, inputEvent.Timestamp);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(14)]
    [InlineData(21)]
    [InlineData(22)]
    [InlineData(23)]
    [InlineData(0x65)]
    [InlineData(0x6D)]
    public void TryCreateSystemDefinedInput_WhenKeyTypeIsUnsupported_ReturnsNoMatchWithoutCodeZeroEvent(int keyType)
    {
        bool created = MacOSInputEventPolicy.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(keyType, 0x0A),
            timestamp: 123,
            timestampMicroseconds: 0,
            out var inputEvent);

        Assert.False(created);
        Assert.Equal(default, inputEvent);
    }

    [Theory]
    [InlineData(10, 8, 16, 0x0A)]
    [InlineData(14, 7, 16, 0x0A)]
    [InlineData(14, 8, 16, 0x09)]
    public void TryCreateSystemDefinedInput_WhenPayloadIsNotSubtype8PressOrRelease_ReturnsNoMatch(
        int eventType,
        long subtype,
        int keyType,
        int state)
    {
        bool created = MacOSInputEventPolicy.TryCreateSystemDefinedInput(
            (CoreGraphics.CGEventType)eventType,
            subtype,
            CreateSystemDefinedData1(keyType, state),
            timestamp: 123,
            timestampMicroseconds: 0,
            out _);

        Assert.False(created);
    }

    [Fact]
    public void CreateHidEventMask_WhenSessionSystemDefinedTapIsUsed_ExcludesSystemDefinedAndIncludesInputEvents()
    {
        var mask = MacOSInputEventPolicy.CreateHidEventMask(useSessionSystemDefinedTap: true);

        Assert.True(ContainsEvent(mask, CoreGraphics.CGEventType.KeyDown));
        Assert.True(ContainsEvent(mask, CoreGraphics.CGEventType.KeyUp));
        Assert.True(ContainsEvent(mask, CoreGraphics.CGEventType.FlagsChanged));
        Assert.True(ContainsEvent(mask, CoreGraphics.CGEventType.ScrollWheel));
        Assert.False(ContainsEvent(mask, CoreGraphics.CGEventType.SystemDefined));
    }

    [Fact]
    public void CreateHidEventMask_WhenSessionSystemDefinedTapIsUnavailable_IncludesSystemDefinedFallback()
    {
        var mask = MacOSInputEventPolicy.CreateHidEventMask(useSessionSystemDefinedTap: false);

        Assert.True(ContainsEvent(mask, CoreGraphics.CGEventType.SystemDefined));
    }

    [Fact]
    public void CreateSystemDefinedEventMask_IncludesOnlySystemDefined()
    {
        var mask = MacOSInputEventPolicy.CreateSystemDefinedEventMask();

        Assert.True(ContainsEvent(mask, CoreGraphics.CGEventType.SystemDefined));
        Assert.False(ContainsEvent(mask, CoreGraphics.CGEventType.KeyDown));
        Assert.False(ContainsEvent(mask, CoreGraphics.CGEventType.MouseMoved));
    }

    [Theory]
    [InlineData(InputEventCode.REL_WHEEL, 2)]
    [InlineData(InputEventCode.REL_HWHEEL, -3)]
    public void TryCreateScrollInput_MapsBothScrollAxesAndPreservesMonotonicTimestamp(ushort code, int value)
    {
        bool created = MacOSInputEventPolicy.TryCreateScrollInput(
            code,
            value,
            timestamp: 123,
            timestampMicroseconds: 123_456,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventType.MouseScroll, inputEvent.Type);
        Assert.Equal(code, inputEvent.Code);
        Assert.Equal(value, inputEvent.Value);
        Assert.Equal(123, inputEvent.Timestamp);
        Assert.Equal(123_456, inputEvent.TimestampMicroseconds);
    }

    [Fact]
    public void TryCreateScrollInput_WithZeroOrUnknownAxis_ReturnsNoEvent()
    {
        Assert.False(MacOSInputEventPolicy.TryCreateScrollInput(InputEventCode.REL_WHEEL, 0, 1, 1, out _));
        Assert.False(MacOSInputEventPolicy.TryCreateScrollInput(InputEventCode.REL_X, 1, 1, 1, out _));
    }

    [Theory]
    [InlineData(2, MouseButtonCode.Middle)]
    [InlineData(3, MouseButtonCode.Side1)]
    [InlineData(4, MouseButtonCode.Side2)]
    public void TryMapOtherMouseButton_UsesCoreGraphicsButtonNumbers(long buttonNumber, int expectedButton)
    {
        bool mapped = MacOSInputEventPolicy.TryMapOtherMouseButton(buttonNumber, out int button);

        Assert.True(mapped);
        Assert.Equal(expectedButton, button);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void TryMapOtherMouseButton_WhenUnsupported_ReturnsFalse(long buttonNumber)
    {
        Assert.False(MacOSInputEventPolicy.TryMapOtherMouseButton(buttonNumber, out _));
    }

    private static long CreateSystemDefinedData1(int keyType, int state) => (keyType << 16) | (state << 8);

    private static bool ContainsEvent(ulong mask, CoreGraphics.CGEventType eventType) =>
        (mask & (1UL << (int)eventType)) != 0;

    private static CapturedInputEvent CreateFlagsChanged(ushort nativeKey, ulong flags)
    {
        bool created = MacOSInputEventPolicy.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.FlagsChanged,
            nativeKey,
            (CoreGraphics.CGEventModifiers)flags,
            timestamp: 100,
            timestampMicroseconds: 200,
            out var inputEvent);

        Assert.True(created);
        return inputEvent;
    }
}
