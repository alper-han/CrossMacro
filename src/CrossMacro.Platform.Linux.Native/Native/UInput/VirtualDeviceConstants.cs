
namespace CrossMacro.Platform.Linux.Native.UInput;

/// <summary>
/// Identity of the uinput device CrossMacro creates, which capture uses to ignore its own simulated input.
/// The vendor and product ids are not registered with the USB-IF. They stay fixed so an older daemon that is
/// still running keeps being recognised as CrossMacro's own device by a newer client.
/// </summary>
public static class VirtualDeviceConstants
{
    public const ushort VendorId = 0x1234;
    public const ushort ProductId = 0x5678;
    public const ushort Version = 1;
    public const int MaxKeyCode = 255;

    public const string DeviceName = "CrossMacro Virtual Input Device";

    public static bool IsCrossMacroVirtualDeviceName(string? deviceName)
    {
        return string.Equals(deviceName, DeviceName, StringComparison.Ordinal);
    }

    public static bool IsCrossMacroVirtualDevice(string? deviceName, ushort vendorId, ushort productId)
    {
        return IsCrossMacroVirtualDeviceName(deviceName) && vendorId == VendorId && productId == ProductId;
    }
}
