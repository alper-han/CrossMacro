
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

    [Fact]
    public void GetKeyName_WhenPauseKey_ReturnsPause()
    {
        var name = _service.GetKeyName(InputEventCode.KEY_PAUSE);

        Assert.Equal("Pause", name);
    }

    [Fact]
    public void GetKeyName_WhenPrintScreenKey_ReturnsPrintScreen()
    {
        var name = _service.GetKeyName(InputEventCode.KEY_SYSRQ);

        Assert.Equal("PrintScreen", name);
    }

    [Fact]
    public void GetKeyName_WhenNumLockKey_ReturnsNumLock()
    {
        var name = _service.GetKeyName(InputEventCode.KEY_NUMLOCK);

        Assert.Equal("NumLock", name);
    }

    [Fact]
    public void GetKeyName_WhenScrollLockKey_ReturnsScrollLock()
    {
        var name = _service.GetKeyName(InputEventCode.KEY_SCROLLLOCK);

        Assert.Equal("ScrollLock", name);
    }

    [Fact]
    public void GetKeyName_WhenLeftModifierKeys_ReturnsExpectedNames()
    {
        Assert.Equal("LeftShift", _service.GetKeyName(InputEventCode.KEY_LEFTSHIFT));
        Assert.Equal("LeftCtrl", _service.GetKeyName(InputEventCode.KEY_LEFTCTRL));
        Assert.Equal("LeftAlt", _service.GetKeyName(InputEventCode.KEY_LEFTALT));
        Assert.Equal("LeftWin", _service.GetKeyName(InputEventCode.KEY_LEFTMETA));
    }

    [Fact]
    public void GetKeyName_WhenRightModifierKeys_ReturnsExpectedNames()
    {
        Assert.Equal("RightShift", _service.GetKeyName(InputEventCode.KEY_RIGHTSHIFT));
        Assert.Equal("RightCtrl", _service.GetKeyName(InputEventCode.KEY_RIGHTCTRL));
        Assert.Equal("RightAlt", _service.GetKeyName(InputEventCode.KEY_RIGHTALT));
        Assert.Equal("RightWin", _service.GetKeyName(InputEventCode.KEY_RIGHTMETA));
    }

    [Fact]
    public void GetKeyCode_WhenUnknownName_ReturnsMinusOne()
    {
        var code = _service.GetKeyCode("NotAKnownKeyName");

        Assert.Equal(-1, code);
    }
}
