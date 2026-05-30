using System;
using System.Collections.Generic;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.MacOS;
using CrossMacro.Platform.MacOS.Native;
using CrossMacro.Platform.MacOS.Services;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Services;

public class MacOSAssignmentPlaybackRegressionTests
{
    [Theory]
    [InlineData(0x65, InputEventCode.KEY_F9, "F9")]
    [InlineData(0x6D, InputEventCode.KEY_F10, "F10")]
    [InlineData(0x69, InputEventCode.KEY_F13, "F13")]
    [InlineData(0x5A, InputEventCode.KEY_F20, "F20")]
    [InlineData(0x51, InputEventCode.KEY_KPEQUAL, "Numpad=")]
    [InlineData(0x72, InputEventCode.KEY_HELP, "Help")]
    [InlineData(0x5D, InputEventCode.KEY_YEN, "Yen")]
    public void Assignment_WhenSupportedMacKeyIsCaptured_RoundTripsThroughDisplayAndParser(int nativeKeyCode, int expectedCode, string expectedDisplayName)
    {
        var layoutService = CreateLayoutService();
        var parser = new HotkeyParser(new KeyCodeMapper(layoutService));
        var stringBuilder = new HotkeyStringBuilder(new KeyCodeMapper(layoutService));

        bool created = MacOSInputCapture.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            (ushort)nativeKeyCode,
            default,
            timestamp: 123,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventType.Key, inputEvent.Type);
        Assert.Equal(expectedCode, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
        Assert.Equal(expectedDisplayName, layoutService.GetKeyName(inputEvent.Code));
        Assert.Equal(expectedCode, layoutService.GetKeyCode(expectedDisplayName));
        Assert.Equal(expectedCode, parser.Parse(expectedDisplayName).MainKey);
        Assert.Equal(expectedDisplayName, stringBuilder.Build(inputEvent.Code, new HashSet<int>()));
    }

    [Fact]
    public void Assignment_WhenNativeHelpIsCaptured_ResolvesToHelpNotInsert()
    {
        bool created = MacOSInputCapture.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            0x72,
            default,
            timestamp: 123,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventCode.KEY_HELP, inputEvent.Code);
        Assert.NotEqual(InputEventCode.KEY_INSERT, inputEvent.Code);
        Assert.Equal(0x72, KeyMap.ToMacKey(InputEventCode.KEY_HELP));
        Assert.Equal(0x72, KeyMap.ToMacKey(InputEventCode.KEY_INSERT));
    }

    [Fact]
    public void Assignment_WhenF21IsRequested_RemainsUnsupportedForOrdinaryMacKeys()
    {
        var layoutService = CreateLayoutService();
        var parser = new HotkeyParser(new KeyCodeMapper(layoutService));

        Assert.Equal(0xFFFF, KeyMap.ToMacKey(InputEventCode.KEY_F21));
        Assert.Equal(-1, layoutService.GetKeyCode("F21"));
        Assert.Equal(-1, parser.Parse("F21").MainKey);
    }

    [Fact]
    public void Assignment_WhenOrdinaryNativeKeyIsUnknown_ReturnsNoMatchWithoutCodeZeroEvent()
    {
        bool created = MacOSInputCapture.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            0xFFFF,
            default,
            timestamp: 123,
            out _);

        Assert.False(created);
    }

    [Theory]
    [InlineData(0, InputEventCode.KEY_VOLUMEUP, "VolumeUp")]
    [InlineData(1, InputEventCode.KEY_VOLUMEDOWN, "VolumeDown")]
    [InlineData(2, InputEventCode.KEY_BRIGHTNESSUP, "BrightnessUp")]
    [InlineData(3, InputEventCode.KEY_BRIGHTNESSDOWN, "BrightnessDown")]
    [InlineData(7, InputEventCode.KEY_MUTE, "Mute")]
    [InlineData(16, InputEventCode.KEY_PLAYPAUSE, "PlayPause")]
    [InlineData(17, InputEventCode.KEY_NEXTSONG, "NextSong")]
    [InlineData(18, InputEventCode.KEY_PREVIOUSSONG, "PreviousSong")]
    [InlineData(19, InputEventCode.KEY_FASTFORWARD, "FastForward")]
    [InlineData(20, InputEventCode.KEY_REWIND, "Rewind")]
    public void Assignment_WhenSupportedSystemDefinedMediaKeyIsCaptured_RoundTripsThroughDisplayAndParser(
        int keyType,
        int expectedCode,
        string expectedDisplayName)
    {
        var layoutService = CreateLayoutService();
        var parser = new HotkeyParser(new KeyCodeMapper(layoutService));
        var stringBuilder = new HotkeyStringBuilder(new KeyCodeMapper(layoutService));

        bool created = MacOSInputCapture.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(keyType, state: 0x0A),
            timestamp: 123,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventType.Key, inputEvent.Type);
        Assert.Equal(expectedCode, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
        Assert.Equal(expectedDisplayName, layoutService.GetKeyName(inputEvent.Code));
        Assert.Equal(expectedCode, layoutService.GetKeyCode(expectedDisplayName));
        Assert.Equal(expectedCode, parser.Parse(expectedDisplayName).MainKey);
        Assert.Equal(expectedDisplayName, stringBuilder.Build(inputEvent.Code, new HashSet<int>()));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(14)]
    [InlineData(21)]
    public void Assignment_WhenSystemDefinedKeyTypeIsUnsupported_ReturnsNoMatchWithoutCodeZeroEvent(int keyType)
    {
        bool created = MacOSInputCapture.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(keyType, state: 0x0A),
            timestamp: 123,
            out _);

        Assert.False(created);
    }

    private static MacKeyboardLayoutService CreateLayoutService()
    {
        return new MacKeyboardLayoutService(
            isMainThread: () => false,
            mainThreadContext: null,
            loadKeyboardLayoutData: () => default,
            warmOnConstruction: false);
    }

    private static long CreateSystemDefinedData1(int keyType, int state)
    {
        return (keyType << 16) | (state << 8);
    }
}
