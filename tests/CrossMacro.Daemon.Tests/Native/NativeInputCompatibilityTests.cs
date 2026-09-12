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
    }

    [Fact]
    public void NativeInputStructsKeepLinuxAbiLayout()
    {
        Assert.Equal((IntPtr.Size * 2) + (sizeof(ushort) * 2) + sizeof(int), Marshal.SizeOf<UInputNative.input_event>());
        Assert.Equal(0, Marshal.OffsetOf<UInputNative.input_event>(nameof(UInputNative.input_event.time_sec)).ToInt32());
        Assert.Equal(IntPtr.Size, Marshal.OffsetOf<UInputNative.input_event>(nameof(UInputNative.input_event.time_usec)).ToInt32());
        Assert.Equal(IntPtr.Size * 2, Marshal.OffsetOf<UInputNative.input_event>(nameof(UInputNative.input_event.type)).ToInt32());
        Assert.Equal((IntPtr.Size * 2) + sizeof(ushort), Marshal.OffsetOf<UInputNative.input_event>(nameof(UInputNative.input_event.code)).ToInt32());
        Assert.Equal((IntPtr.Size * 2) + (sizeof(ushort) * 2), Marshal.OffsetOf<UInputNative.input_event>(nameof(UInputNative.input_event.value)).ToInt32());

        Assert.Equal(8, Marshal.SizeOf<UInputNative.input_id>());
        Assert.Equal(80 + Marshal.SizeOf<UInputNative.input_id>() + sizeof(int) + (4 * 64 * sizeof(int)), Marshal.SizeOf<UInputNative.uinput_user_dev>());
        Assert.Equal(80, Marshal.OffsetOf<UInputNative.uinput_user_dev>(nameof(UInputNative.uinput_user_dev.id_bustype)).ToInt32());
        Assert.Equal(88, Marshal.OffsetOf<UInputNative.uinput_user_dev>(nameof(UInputNative.uinput_user_dev.ff_effects_max)).ToInt32());
        Assert.Equal(92, Marshal.OffsetOf<UInputNative.uinput_user_dev>(nameof(UInputNative.uinput_user_dev.absmax)).ToInt32());

        Assert.Equal(92, Marshal.SizeOf<UInputNative.uinput_setup>());
        Assert.Equal(8, Marshal.OffsetOf<UInputNative.uinput_setup>(nameof(UInputNative.uinput_setup.name)).ToInt32());
        Assert.Equal(88, Marshal.OffsetOf<UInputNative.uinput_setup>(nameof(UInputNative.uinput_setup.ff_effects_max)).ToInt32());
    }

    [Fact]
    public void EvdevIoctlRequestsKeepKernelEncoding()
    {
        Assert.Equal(EvdevNative.EVIOCGBIT_EV, EvdevNative.EVIOCGBIT(UInputNative.EV_SYN, 4));
        Assert.Equal(EvdevNative.EVIOCGBIT_KEY, EvdevNative.EVIOCGBIT(UInputNative.EV_KEY, 4));
        Assert.Equal(EvdevNative.EVIOCGBIT_REL, EvdevNative.EVIOCGBIT(UInputNative.EV_REL, 4));
        Assert.Equal(EvdevNative.EVIOCGBIT_ABS, EvdevNative.EVIOCGBIT(UInputNative.EV_ABS, 4));
    }

    [Fact]
    public void VirtualDeviceIdentifiersKeepExactMatchingContract()
    {
        Assert.True(VirtualDeviceConstants.IsCrossMacroVirtualDevice(
            VirtualDeviceConstants.DeviceName,
            VirtualDeviceConstants.VendorId,
            VirtualDeviceConstants.ProductId));
        Assert.False(VirtualDeviceConstants.IsCrossMacroVirtualDevice(
            VirtualDeviceConstants.DeviceName + " (copy)",
            VirtualDeviceConstants.VendorId,
            VirtualDeviceConstants.ProductId));
        Assert.False(VirtualDeviceConstants.IsCrossMacroVirtualDevice(
            VirtualDeviceConstants.DeviceName,
            vendorId: 0x9999,
            productId: VirtualDeviceConstants.ProductId));
    }
}
