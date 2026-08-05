namespace CrossMacro.Platform.Linux.Native.Evdev;

/// <summary>
/// Pure input-device classification rules shared by discovery and diagnostics.
/// Kernel I/O remains in <see cref="InputDeviceHelper"/>; these rules are internal
/// so the public native ABI stays unchanged while policy remains testable.
/// </summary>
internal static partial class InputDeviceClassification
{
    [GeneratedRegex(@"\bmouse\d+\b", RegexOptions.NonBacktracking)]
    private static partial Regex MouseHandlerRegex { get; }

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

    public static bool HasKernelHandler(string devicePath, string deviceName, string? procContent, string handlerType)
    {
        if (string.IsNullOrEmpty(procContent))
        {
            return false;
        }

        var eventName = Path.GetFileName(devicePath);
        foreach (var block in procContent.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!block.Contains(eventName, StringComparison.Ordinal))
            {
                continue;
            }

            var nameMatches = false;
            var hasHandler = false;

            using var reader = new StringReader(block);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line.StartsWith("N: Name=", StringComparison.Ordinal) && line.Contains(deviceName, StringComparison.Ordinal))
                {
                    nameMatches = true;
                }

                if (line.StartsWith("H: Handlers=", StringComparison.Ordinal) && line.Contains(eventName, StringComparison.Ordinal))
                {
                    hasHandler = string.Equals(handlerType, "mouse", StringComparison.Ordinal)
                        ? MouseHandlerRegex.IsMatch(line)
                        : line.Contains("kbd", StringComparison.Ordinal);
                }
            }

            if (nameMatches && hasHandler)
            {
                return true;
            }
        }

        return false;
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
