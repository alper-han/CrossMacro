namespace CrossMacro.Daemon.Tests.Native;

public sealed class NativeInputCompatibilityTests
{
    [Fact]
    public void NativeInputTypesKeepExpectedNamespacesAndAbiConstants()
    {
        Assert.Equal("/dev/uinput", LinuxSystemPaths.UInputDevicePath);
        Assert.Equal("/dev/input/uinput", LinuxSystemPaths.UInputAlternatePath);
        Assert.Equal(UInputNative.BTN_LEFT, (ushort)0x110);
        Assert.True(UInputNative.IsMouseButton(UInputNative.BTN_LEFT));
        Assert.True(UInputNative.IsMouseButton(UInputNative.BTN_TASK));
        Assert.False(UInputNative.IsMouseButton(0x10f));
        Assert.Equal((ulong)0x80044521, EvdevNative.EVIOCGBIT(UInputNative.EV_KEY, 4));
    }
}
