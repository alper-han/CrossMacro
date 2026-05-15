using System;

namespace CrossMacro.Infrastructure.Linux.Native.UInput;

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
