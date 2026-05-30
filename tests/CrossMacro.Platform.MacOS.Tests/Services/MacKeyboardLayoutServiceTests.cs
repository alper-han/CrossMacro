using CrossMacro.Platform.MacOS;
using CrossMacro.Platform.Abstractions;
using System;
using System.Threading;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Services;

public class MacKeyboardLayoutServiceTests
{
    private readonly MacKeyboardLayoutService _service = new();

    [Theory]
    [InlineData("Ctrl", InputEventCode.KEY_LEFTCTRL)]
    [InlineData("Shift", InputEventCode.KEY_LEFTSHIFT)]
    [InlineData("Alt", InputEventCode.KEY_LEFTALT)]
    [InlineData("Option", InputEventCode.KEY_LEFTALT)]
    [InlineData("Command", InputEventCode.KEY_LEFTMETA)]
    [InlineData("Super", InputEventCode.KEY_LEFTMETA)]
    [InlineData("Enter", InputEventCode.KEY_ENTER)]
    [InlineData("Tab", InputEventCode.KEY_TAB)]
    [InlineData("Escape", InputEventCode.KEY_ESC)]
    [InlineData("Home", InputEventCode.KEY_HOME)]
    [InlineData("F1", InputEventCode.KEY_F1)]
    [InlineData("F12", InputEventCode.KEY_F12)]
    [InlineData("F20", InputEventCode.KEY_F20)]
    [InlineData("Numpad=", InputEventCode.KEY_KPEQUAL)]
    [InlineData("NumpadPlus", InputEventCode.KEY_KPPLUS)]
    [InlineData("Help", InputEventCode.KEY_HELP)]
    [InlineData("Mute", InputEventCode.KEY_MUTE)]
    [InlineData("VolumeDown", InputEventCode.KEY_VOLUMEDOWN)]
    [InlineData("VolumeUp", InputEventCode.KEY_VOLUMEUP)]
    [InlineData("BrightnessDown", InputEventCode.KEY_BRIGHTNESSDOWN)]
    [InlineData("BrightnessUp", InputEventCode.KEY_BRIGHTNESSUP)]
    [InlineData("PlayPause", InputEventCode.KEY_PLAYPAUSE)]
    [InlineData("PreviousSong", InputEventCode.KEY_PREVIOUSSONG)]
    [InlineData("NextSong", InputEventCode.KEY_NEXTSONG)]
    [InlineData("Rewind", InputEventCode.KEY_REWIND)]
    [InlineData("FastForward", InputEventCode.KEY_FASTFORWARD)]
    [InlineData("Yen", InputEventCode.KEY_YEN)]
    [InlineData("NumpadJpComma", InputEventCode.KEY_KPJPCOMMA)]
    public void GetKeyCode_WhenUsingKnownNames_ReturnsExpectedCode(string keyName, int expected)
    {
        var code = _service.GetKeyCode(keyName);

        Assert.Equal(expected, code);
    }

    [Fact]
    public void GetKeyCode_WhenUsingUnknownName_ReturnsMinusOne()
    {
        var code = _service.GetKeyCode("UnknownKey");

        Assert.Equal(-1, code);
    }

    [Theory]
    [InlineData(InputEventCode.KEY_LEFTCTRL, "Ctrl")]
    [InlineData(InputEventCode.KEY_RIGHTCTRL, "Ctrl")]
    [InlineData(InputEventCode.KEY_LEFTSHIFT, "Shift")]
    [InlineData(InputEventCode.KEY_RIGHTSHIFT, "Shift")]
    [InlineData(InputEventCode.KEY_LEFTALT, "Alt")]
    [InlineData(InputEventCode.KEY_RIGHTALT, "Alt")]
    [InlineData(InputEventCode.KEY_LEFTMETA, "Command")]
    [InlineData(InputEventCode.KEY_RIGHTMETA, "Command")]
    [InlineData(InputEventCode.KEY_F1, "F1")]
    [InlineData(InputEventCode.KEY_UP, "Up")]
    [InlineData(InputEventCode.KEY_KPEQUAL, "Numpad=")]
    [InlineData(InputEventCode.KEY_KPPLUS, "NumpadPlus")]
    [InlineData(InputEventCode.KEY_HELP, "Help")]
    [InlineData(InputEventCode.KEY_MUTE, "Mute")]
    [InlineData(InputEventCode.KEY_VOLUMEDOWN, "VolumeDown")]
    [InlineData(InputEventCode.KEY_VOLUMEUP, "VolumeUp")]
    [InlineData(InputEventCode.KEY_BRIGHTNESSDOWN, "BrightnessDown")]
    [InlineData(InputEventCode.KEY_BRIGHTNESSUP, "BrightnessUp")]
    [InlineData(InputEventCode.KEY_PLAYPAUSE, "PlayPause")]
    [InlineData(InputEventCode.KEY_PREVIOUSSONG, "PreviousSong")]
    [InlineData(InputEventCode.KEY_NEXTSONG, "NextSong")]
    [InlineData(InputEventCode.KEY_REWIND, "Rewind")]
    [InlineData(InputEventCode.KEY_FASTFORWARD, "FastForward")]
    [InlineData(InputEventCode.KEY_YEN, "Yen")]
    [InlineData(InputEventCode.KEY_KPJPCOMMA, "NumpadJpComma")]
    public void GetKeyName_WhenUsingKnownCodes_ReturnsExpectedName(int keyCode, string expected)
    {
        var name = _service.GetKeyName(keyCode);

        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData(InputEventCode.KEY_F9)]
    [InlineData(InputEventCode.KEY_F10)]
    [InlineData(InputEventCode.KEY_F11)]
    [InlineData(InputEventCode.KEY_F12)]
    [InlineData(InputEventCode.KEY_F13)]
    [InlineData(InputEventCode.KEY_F20)]
    [InlineData(InputEventCode.KEY_KP0)]
    [InlineData(InputEventCode.KEY_KP1)]
    [InlineData(InputEventCode.KEY_KPPLUS)]
    [InlineData(InputEventCode.KEY_KPEQUAL)]
    [InlineData(InputEventCode.KEY_HELP)]
    [InlineData(InputEventCode.KEY_MUTE)]
    [InlineData(InputEventCode.KEY_VOLUMEDOWN)]
    [InlineData(InputEventCode.KEY_VOLUMEUP)]
    [InlineData(InputEventCode.KEY_BRIGHTNESSDOWN)]
    [InlineData(InputEventCode.KEY_BRIGHTNESSUP)]
    [InlineData(InputEventCode.KEY_PLAYPAUSE)]
    [InlineData(InputEventCode.KEY_PREVIOUSSONG)]
    [InlineData(InputEventCode.KEY_NEXTSONG)]
    [InlineData(InputEventCode.KEY_REWIND)]
    [InlineData(InputEventCode.KEY_FASTFORWARD)]
    [InlineData(InputEventCode.KEY_YEN)]
    [InlineData(InputEventCode.KEY_KPJPCOMMA)]
    public void DisplayNames_WhenMacSupportedKeysInTaskScope_ParseBackToSameCode(int keyCode)
    {
        var displayName = _service.GetKeyName(keyCode);
        var parsedCode = _service.GetKeyCode(displayName);

        Assert.Equal(keyCode, parsedCode);
    }

    [Theory]
    [InlineData("F21")]
    [InlineData("F22")]
    [InlineData("F23")]
    [InlineData("F24")]
    [InlineData("Menu")]
    public void GetKeyCode_WhenNameHasNoSupportedMacOrdinaryMapping_ReturnsMinusOne(string keyName)
    {
        var code = _service.GetKeyCode(keyName);

        Assert.Equal(-1, code);
    }

    [Fact]
    public void GetCharFromKeyCode_WhenModifier_ReturnsNull()
    {
        var result = _service.GetCharFromKeyCode(
            InputEventCode.KEY_LEFTCTRL,
            leftShift: false,
            rightShift: false,
            rightAlt: false,
            leftAlt: false,
            leftCtrl: false,
            capsLock: false);

        Assert.Null(result);
    }

    [Fact]
    public void GetCharFromKeyCode_WhenSpace_ReturnsSpaceCharacter()
    {
        var result = _service.GetCharFromKeyCode(
            InputEventCode.KEY_SPACE,
            leftShift: false,
            rightShift: false,
            rightAlt: false,
            leftAlt: false,
            leftCtrl: false,
            capsLock: false);

        Assert.Equal(' ', result);
    }

    [Fact]
    public void GetCharFromKeyCode_WhenLayoutCacheMissingOffMainThread_DoesNotLoadNativeLayout()
    {
        var loadCount = 0;
        var service = new MacKeyboardLayoutService(
            isMainThread: () => false,
            mainThreadContext: null,
            loadKeyboardLayoutData: () =>
            {
                loadCount++;
                return default;
            },
            warmOnConstruction: false);

        var result = service.GetCharFromKeyCode(
            InputEventCode.KEY_A,
            leftShift: false,
            rightShift: false,
            rightAlt: false,
            leftAlt: false,
            leftCtrl: false,
            capsLock: false);

        Assert.Null(result);
        Assert.Equal(0, loadCount);
    }

    [Fact]
    public void GetCharFromKeyCode_WhenOffMainThreadWithMainContext_LoadsNativeLayoutThroughContext()
    {
        var runningOnMainThread = false;
        var loadCount = 0;
        var context = new InlineMainThreadSynchronizationContext(
            beforeSend: () => runningOnMainThread = true,
            afterSend: () => runningOnMainThread = false);

        var service = new MacKeyboardLayoutService(
            isMainThread: () => runningOnMainThread,
            mainThreadContext: context,
            loadKeyboardLayoutData: () =>
            {
                loadCount++;
                return default;
            },
            warmOnConstruction: false);

        _ = service.GetCharFromKeyCode(
            InputEventCode.KEY_A,
            leftShift: false,
            rightShift: false,
            rightAlt: false,
            leftAlt: false,
            leftCtrl: false,
            capsLock: false);

        Assert.Equal(1, loadCount);
    }

    [Fact]
    public void Constructor_WhenCreatedOnMainThread_WarmsLayoutCache()
    {
        var loadCount = 0;

        _ = new MacKeyboardLayoutService(
            isMainThread: () => true,
            mainThreadContext: null,
            loadKeyboardLayoutData: () =>
            {
                loadCount++;
                return default;
            },
            warmOnConstruction: true);

        Assert.Equal(1, loadCount);
    }

    [Fact]
    public void GetInputForChar_WhenLayoutCacheMissingOffMainThread_DoesNotCacheEmptyMap()
    {
        var isMainThread = false;
        var loadCount = 0;
        var service = new MacKeyboardLayoutService(
            isMainThread: () => isMainThread,
            mainThreadContext: null,
            loadKeyboardLayoutData: () =>
            {
                loadCount++;
                return default;
            },
            warmOnConstruction: false);

        var offMainResult = service.GetInputForChar('a');
        isMainThread = true;
        var mainThreadResult = service.GetInputForChar('a');

        Assert.Null(offMainResult);
        Assert.Null(mainThreadResult);
        Assert.Equal(1, loadCount);
    }

    private sealed class InlineMainThreadSynchronizationContext(Action beforeSend, Action afterSend) : SynchronizationContext
    {
        public override void Send(SendOrPostCallback d, object? state)
        {
            beforeSend();
            try
            {
                d(state);
            }
            finally
            {
                afterSend();
            }
        }
    }
}
