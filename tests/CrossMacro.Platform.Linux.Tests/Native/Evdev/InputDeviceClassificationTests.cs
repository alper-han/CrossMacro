namespace CrossMacro.Platform.Linux.Tests.Native.Evdev;

public sealed class InputDeviceClassificationTests
{
    [Theory]
    [InlineData(false, false, false, "Other")]
    [InlineData(false, true, false, "Mouse")]
    [InlineData(false, false, true, "Keyboard")]
    [InlineData(false, true, true, "Mouse+Keyboard")]
    [InlineData(true, false, false, "Virtual")]
    [InlineData(true, true, false, "Virtual Mouse")]
    [InlineData(true, false, true, "Virtual Keyboard")]
    [InlineData(true, true, true, "Virtual Mouse+Keyboard")]
    public void GetDeviceType_PreservesExistingClassificationLabels(
        bool isVirtual,
        bool isMouse,
        bool isKeyboard,
        string expected)
    {
        Assert.Equal(expected, InputDeviceClassification.GetDeviceType(isVirtual, isMouse, isKeyboard));
    }

    [Theory]
    [InlineData("Power Button")]
    [InlineData("AT Translated Set 2 keyboard Consumer Control")]
    [InlineData("AT Translated Set 2 keyboard System Control")]
    [InlineData("WMI hotkeys")]
    [InlineData("Bluetooth AVRCP")]
    public void ShouldExclude_RejectsNonInputDevices(string name)
    {
        Assert.True(InputDeviceClassification.ShouldExclude(name));
    }

    [Theory]
    [InlineData("USB Mouse")]
    [InlineData("AT Translated Set 2 keyboard")]
    public void ShouldExclude_KeepsCaptureDevices(string name)
    {
        Assert.False(InputDeviceClassification.ShouldExclude(name));
    }

    [Fact]
    public void HasKernelHandler_RecognizesMouseAndKeyboardHandlersForTheMatchingEvent()
    {
        const string procDevices =
            "I: Bus=0003 Vendor=046d Product=c077 Version=0111\n" +
            "N: Name=\"USB Mouse\"\n" +
            "H: Handlers=mouse0 event5\n\n" +
            "I: Bus=0003 Vendor=046d Product=c31c Version=0111\n" +
            "N: Name=\"USB Keyboard\"\n" +
            "H: Handlers=sysrq kbd event6\n";

        Assert.True(InputDeviceClassification.HasKernelHandler("/dev/input/event5", "USB Mouse", procDevices, "mouse"));
        Assert.True(InputDeviceClassification.HasKernelHandler("/dev/input/event6", "USB Keyboard", procDevices, "kbd"));
        Assert.False(InputDeviceClassification.HasKernelHandler("/dev/input/event5", "USB Keyboard", procDevices, "kbd"));
        Assert.False(InputDeviceClassification.HasKernelHandler("/dev/input/event7", "USB Mouse", procDevices, "mouse"));
    }

    [Theory]
    [InlineData((ushort)0x03, "USB")]
    [InlineData((ushort)0x06, "Virtual")]
    [InlineData((ushort)0xFF, "Unknown(0xFF)")]
    public void GetBusTypeName_UsesStableDiagnosticLabels(ushort busType, string expected)
    {
        Assert.Equal(expected, InputDeviceClassification.GetBusTypeName(busType));
    }
}
