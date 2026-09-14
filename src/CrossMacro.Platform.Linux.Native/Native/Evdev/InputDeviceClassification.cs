namespace CrossMacro.Platform.Linux.Native.Evdev;

/// <summary>
/// Pure input-device classification rules shared by discovery and diagnostics.
/// Kernel I/O remains in <see cref="InputDeviceHelper"/>; these rules are internal
/// so the public native ABI stays unchanged while policy remains testable.
/// </summary>
internal static class InputDeviceClassification
{
    public static string GetDeviceType(bool isVirtual, bool isMouse, bool isKeyboard)
    {
        if (isVirtual && isMouse && isKeyboard)
        {
            return "Virtual Mouse+Keyboard";
        }

        if (isVirtual && isMouse)
        {
            return "Virtual Mouse";
        }

        if (isVirtual && isKeyboard)
        {
            return "Virtual Keyboard";
        }

        if (isVirtual)
        {
            return "Virtual";
        }

        if (isMouse && isKeyboard)
        {
            return "Mouse+Keyboard";
        }

        if (isMouse)
        {
            return "Mouse";
        }

        if (isKeyboard)
        {
            return "Keyboard";
        }

        return "Other";
    }

    public static bool ShouldExclude(string name)
    {
        if (name.Equals("Power Button", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Sleep Button", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Video Bus", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Lid Switch", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.EndsWith(" Consumer Control", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(" System Control", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.Contains("WMI", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("hotkeys", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return name.Contains("AVRCP", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetBusTypeName(ushort busType)
    {
        return busType switch
        {
            0x01 => "PCI",
            0x02 => "ISA",
            0x03 => "USB",
            0x04 => "HIL",
            0x05 => "Bluetooth",
            0x06 => "Virtual",
            0x10 => "ISA_Plug_and_Play",
            0x11 => "USB_HID",
            0x18 => "I2C",
            0x19 => "Host",
            0x1A => "GSC",
            0x1B => "Atari",
            0x1C => "SPI",
            0x1D => "RMI",
            0x1E => "CEC",
            0x1F => "Intel_ISHTP",
            _ => $"Unknown(0x{busType:X2})",
        };
    }
}
