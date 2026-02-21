using CrossMacro.Core.Services;
using CrossMacro.Platform.MacOS;
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
    public void GetKeyName_WhenUsingKnownCodes_ReturnsExpectedName(int keyCode, string expected)
    {
        var name = _service.GetKeyName(keyCode);

        Assert.Equal(expected, name);
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
}
