using CrossMacro.Core.Services;
using CrossMacro.Platform.Windows.Services;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Windows.Tests.Services;

public class WindowsKeyboardLayoutServiceTests
{
    private readonly WindowsKeyboardLayoutService _service = new();

    [WindowsFact]
    public void GetKeyName_WhenUnknownEvdevCode_ReturnsFallbackKeyLabel()
    {
        var name = _service.GetKeyName(9999);

        Assert.Equal("Key_9999", name);
    }

    [WindowsFact]
    public void GetKeyName_WhenPauseKey_ReturnsPause()
    {
        var name = _service.GetKeyName(InputEventCode.KEY_PAUSE);

        Assert.Equal("Pause", name);
    }

    [WindowsFact]
    public void GetKeyName_WhenPrintScreenKey_ReturnsPrintScreen()
    {
        var name = _service.GetKeyName(InputEventCode.KEY_SYSRQ);

        Assert.Equal("PrintScreen", name);
    }

    [WindowsFact]
    public void GetKeyName_WhenNumLockKey_ReturnsNumLock()
    {
        var name = _service.GetKeyName(InputEventCode.KEY_NUMLOCK);

        Assert.Equal("NumLock", name);
    }

    [WindowsFact]
    public void GetKeyName_WhenScrollLockKey_ReturnsScrollLock()
    {
        var name = _service.GetKeyName(InputEventCode.KEY_SCROLLLOCK);

        Assert.Equal("ScrollLock", name);
    }

    [WindowsFact]
    public void GetKeyName_WhenLeftModifierKeys_ReturnsExpectedNames()
    {
        Assert.Equal("LeftShift", _service.GetKeyName(InputEventCode.KEY_LEFTSHIFT));
        Assert.Equal("LeftCtrl", _service.GetKeyName(InputEventCode.KEY_LEFTCTRL));
        Assert.Equal("LeftAlt", _service.GetKeyName(InputEventCode.KEY_LEFTALT));
        Assert.Equal("LeftWin", _service.GetKeyName(InputEventCode.KEY_LEFTMETA));
    }

    [WindowsFact]
    public void GetKeyName_WhenRightModifierKeys_ReturnsExpectedNames()
    {
        Assert.Equal("RightShift", _service.GetKeyName(InputEventCode.KEY_RIGHTSHIFT));
        Assert.Equal("RightCtrl", _service.GetKeyName(InputEventCode.KEY_RIGHTCTRL));
        Assert.Equal("RightAlt", _service.GetKeyName(InputEventCode.KEY_RIGHTALT));
        Assert.Equal("RightWin", _service.GetKeyName(InputEventCode.KEY_RIGHTMETA));
    }

    [WindowsFact]
    public void GetKeyCode_WhenUnknownName_ReturnsZero()
    {
        var code = _service.GetKeyCode("NotAKnownKeyName");

        Assert.Equal(0, code);
    }
}
