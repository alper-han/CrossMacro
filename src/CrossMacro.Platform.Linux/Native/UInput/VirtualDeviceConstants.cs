using System;

namespace CrossMacro.Platform.Linux.Native.UInput;

/// <summary>
/// Constants used for the virtual input device creation.
/// </summary>
public static class VirtualDeviceConstants
{
    public const ushort VendorId = 0x1234;
    public const ushort ProductId = 0x5678;
    public const ushort Version = 1;
    public const int MaxKeyCode = 255;
    
    // Naming constants
    public const string DeviceName = "CrossMacro Virtual Input Device";

    public static bool IsCrossMacroVirtualDeviceName(string? deviceName)
    {
        return string.Equals(deviceName, DeviceName, StringComparison.Ordinal);
    }
}
