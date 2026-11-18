
namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class WindowsKeyboardLayoutServiceTests
{
    private readonly WindowsKeyboardLayoutService _service = new();

    [Fact]
    public void GetKeyName_WhenUnknownEvdevCode_ReturnsFallbackKeyLabel()
    {
        var name = _service.GetKeyName(9999);

        Assert.Equal("Key_9999", name);
    }

    [Theory]
    [InlineData(InputEventCode.KEY_PAUSE, "Pause")]
    [InlineData(InputEventCode.KEY_SYSRQ, "PrintScreen")]
    [InlineData(InputEventCode.KEY_NUMLOCK, "NumLock")]
    [InlineData(InputEventCode.KEY_SCROLLLOCK, "ScrollLock")]
    [InlineData(InputEventCode.KEY_LEFTSHIFT, "LeftShift")]
    [InlineData(InputEventCode.KEY_LEFTCTRL, "LeftCtrl")]
    [InlineData(InputEventCode.KEY_LEFTALT, "LeftAlt")]
    [InlineData(InputEventCode.KEY_LEFTMETA, "LeftWin")]
    [InlineData(InputEventCode.KEY_RIGHTSHIFT, "RightShift")]
    [InlineData(InputEventCode.KEY_RIGHTCTRL, "RightCtrl")]
    [InlineData(InputEventCode.KEY_RIGHTALT, "RightAlt")]
    [InlineData(InputEventCode.KEY_RIGHTMETA, "RightWin")]
    public void GetKeyName_ForNonPrintableKeys_ReturnsExpectedName(int keyCode, string expected)
    {
        Assert.Equal(expected, _service.GetKeyName(keyCode));
    }

    [Fact]
    public void GetKeyCode_WhenUnknownName_ReturnsMinusOne()
    {
        var code = _service.GetKeyCode("NotAKnownKeyName");

        Assert.Equal(-1, code);
    }
}
