using CrossMacro.Platform.Windows.Helpers;

namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class WindowsKeyMapTests
{
    [Fact]
    public void GetEvdevCode_WhenVirtualKeyIsNormalReturn_MapsToKeyEnter()
    {
        var evdevCode = WindowsKeyMap.GetEvdevCode(VirtualKey.Return);

        Assert.Equal(InputEventCode.KEY_ENTER, evdevCode);
        Assert.NotEqual(InputEventCode.KEY_KPENTER, evdevCode);
    }

    [Fact]
    public void WindowsKeyMap_WhenIsoSectionCodeIsUsed_MapsToOem102()
    {
        Assert.Equal(VirtualKey.Oem102, WindowsKeyMap.GetVirtualKey(InputEventCode.KEY_102ND));
        Assert.Equal(InputEventCode.KEY_102ND, WindowsKeyMap.GetEvdevCode(VirtualKey.Oem102));
    }

    [Theory]
    [InlineData(InputEventCode.KEY_CAPSLOCK, VirtualKey.Capital)]
    [InlineData(InputEventCode.KEY_SCROLLLOCK, VirtualKey.ScrollLock)]
    [InlineData(InputEventCode.KEY_NUMLOCK, VirtualKey.NumLock)]
    [InlineData(InputEventCode.KEY_SYSRQ, VirtualKey.Snapshot)]
    [InlineData(InputEventCode.KEY_LEFTSHIFT, VirtualKey.LeftShift)]
    [InlineData(InputEventCode.KEY_RIGHTMETA, VirtualKey.RightWindows)]
    [InlineData(InputEventCode.KEY_KPSLASH, VirtualKey.Divide)]
    [InlineData(InputEventCode.KEY_F24, VirtualKey.F24)]
    [InlineData(InputEventCode.KEY_PLAYPAUSE, VirtualKey.MediaPlayPause)]
    public void WindowsKeyMap_ForKeyInBothTables_RoundTrips(int evdev, ushort virtualKey)
    {
        Assert.Equal(virtualKey, WindowsKeyMap.GetVirtualKey(evdev));
        Assert.Equal(evdev, WindowsKeyMap.GetEvdevCode(virtualKey));
    }

    [Fact]
    public void WindowsKeyMap_KeypadEnter_SharesReturnVirtualKeyAndDoesNotOwnTheReverseMapping()
    {
        Assert.Equal(VirtualKey.Return, WindowsKeyMap.GetVirtualKey(InputEventCode.KEY_KPENTER));
        Assert.Equal(InputEventCode.KEY_ENTER, WindowsKeyMap.GetEvdevCode(VirtualKey.Return));
    }

    [Theory]
    [InlineData(InputEventCode.KEY_KPENTER, true)]
    [InlineData(InputEventCode.KEY_KPSLASH, true)]
    [InlineData(InputEventCode.KEY_RIGHTALT, true)]
    [InlineData(InputEventCode.KEY_PAGEUP, true)]
    [InlineData(InputEventCode.KEY_MENU, true)]
    [InlineData(InputEventCode.KEY_ENTER, false)]
    [InlineData(InputEventCode.KEY_LEFTALT, false)]
    [InlineData(InputEventCode.KEY_KP7, false)]
    [InlineData(InputEventCode.KEY_RIGHTSHIFT, false)]
    [InlineData(0, false)]
    public void NeedsExtendedFlag_ReflectsTheScanCodePrefix(int evdev, bool expected)
    {
        Assert.Equal(expected, WindowsKeyMap.NeedsExtendedFlag(evdev));
    }
}
