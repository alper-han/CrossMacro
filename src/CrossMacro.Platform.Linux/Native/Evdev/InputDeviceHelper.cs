using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CrossMacro.Platform.Linux.Native.UInput;
using CrossMacro.Platform.Linux.Services;
using Serilog;

namespace CrossMacro.Platform.Linux.Native.Evdev;

public class InputDeviceHelper
{
    private static readonly Regex MouseHandlerRegex = new(@"\bmouse\d+\b", RegexOptions.Compiled);

    public class InputDevice
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsMouse { get; set; }
        public bool IsKeyboard { get; set; }
        public bool IsVirtual { get; set; }
        public ushort VendorId { get; set; }
        public ushort ProductId { get; set; }
        public ushort BusType { get; set; }
        public ushort Version { get; set; }

        public string DeviceType
        {
            get
            {
                if (IsVirtual) return "Virtual";
                if (IsMouse && IsKeyboard) return "Mouse+Keyboard";
                if (IsMouse) return "Mouse";
                if (IsKeyboard) return "Keyboard";
                return "Other";
            }
        }

        public override string ToString() =>
            $"{Name} ({Path}) [{DeviceType}] VID:0x{VendorId:X4} PID:0x{ProductId:X4}";
    }

    public static List<InputDevice> GetAvailableDevices()
    {
        List<InputDevice> devices = [];
        List<InputDevice> skippedDevices = [];
        List<InputDevice> inaccessibleDevices = [];
        var inputDir = "/dev/input";

        Log.Information("[InputDeviceHelper] Scanning input devices in {InputDir}...", inputDir);

        if (!Directory.Exists(inputDir))
        {
            Log.Warning("[InputDeviceHelper] Directory {InputDir} does not exist.", inputDir);
            return devices;
        }

        var files = Directory.GetFiles(inputDir, "event*");
        Log.Debug("[InputDeviceHelper] Found {Count} event files to analyze.", files.Length);

        string? procDevicesContent = null;
        try
        {
            if (File.Exists("/proc/bus/input/devices"))
                procDevicesContent = File.ReadAllText("/proc/bus/input/devices");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[InputDeviceHelper] Failed to read /proc/bus/input/devices");
        }

        foreach (var file in files)
        {
            try
            {
                var device = GetDeviceInfo(file, procDevicesContent);

                if (device.IsMouse || device.IsKeyboard)
                {
                    if (CanOpenForReading(file))
                    {
                        devices.Add(device);
                    }
                    else
                    {
                        // Input device detected but cannot be opened (permission issue)
                        inaccessibleDevices.Add(device);
                    }
                }
                else
                {
                    skippedDevices.Add(device);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[InputDeviceHelper] Error reading {File}", file);
            }
        }

        // Log summary
        Log.Information("[InputDeviceHelper] ========== Device Summary ==========");
        Log.Information("[InputDeviceHelper] Total: {Total} | Usable: {Usable} | Inaccessible: {Inaccessible} | Skipped: {Skipped}",
            files.Length, devices.Count, inaccessibleDevices.Count, skippedDevices.Count);

        if (devices.Count > 0)
        {
            Log.Information("[InputDeviceHelper] --- Active Input Devices ---");
            foreach (var dev in devices)
            {
                Log.Information("[InputDeviceHelper]   [{Type}] {Name} ({Path}) | Bus: {Bus} | VID:0x{VID:X4} PID:0x{PID:X4}",
                    dev.DeviceType, dev.Name, dev.Path, GetBusTypeName(dev.BusType), dev.VendorId, dev.ProductId);
            }
        }

        if (inaccessibleDevices.Count > 0)
        {
            Log.Warning("[InputDeviceHelper] --- Inaccessible Devices (permission denied?) ---");
            foreach (var dev in inaccessibleDevices)
            {
                Log.Warning("[InputDeviceHelper]   [{Type}] {Name} ({Path}) | VID:0x{VID:X4} PID:0x{PID:X4} - Cannot open for reading!",
                    dev.DeviceType, dev.Name, dev.Path, dev.VendorId, dev.ProductId);
            }
        }

        if (skippedDevices.Count > 0)
        {
            Log.Debug("[InputDeviceHelper] --- Skipped Devices (not input devices) ---");
            foreach (var dev in skippedDevices)
            {
                Log.Debug("[InputDeviceHelper]   [{Type}] {Name} ({Path}) | VID:0x{VID:X4} PID:0x{PID:X4}",
                    dev.DeviceType, dev.Name, dev.Path, dev.VendorId, dev.ProductId);
            }
        }

        Log.Information("[InputDeviceHelper] ====================================");

        return devices;
    }

    private static InputDevice GetDeviceInfo(string devicePath, string? procDevicesContent)
    {
        int fd = EvdevNative.open(devicePath, EvdevNative.O_RDONLY);
        if (fd < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            throw new IOException($"Cannot open {devicePath}. Errno: {errno}");
        }

        try
        {
            byte[] nameBuf = new byte[256];
            EvdevNative.ioctl(fd, EvdevNative.EVIOCGNAME_256, nameBuf);
            string name = System.Text.Encoding.ASCII.GetString(nameBuf).TrimEnd('\0');

            // Read device ID (VID/PID)
            var (busType, vendorId, productId, version) = ReadDeviceId(fd);

            if (IsVirtualDevice(devicePath, name))
            {
                Log.Debug("[InputDeviceHelper] Virtual device: {Path} - {Name} (VID:0x{VID:X4} PID:0x{PID:X4})",
                    devicePath, name, vendorId, productId);
                return new InputDevice
                {
                    Path = devicePath,
                    Name = name,
                    IsMouse = false,
                    IsKeyboard = false,
                    IsVirtual = true,
                    BusType = busType,
                    VendorId = vendorId,
                    ProductId = productId,
                    Version = version
                };
            }

            if (ShouldExcludeDevice(name))
            {
                Log.Debug("[InputDeviceHelper] Excluded device: {Path} - {Name} (VID:0x{VID:X4} PID:0x{PID:X4})",
                    devicePath, name, vendorId, productId);
                return new InputDevice
                {
                    Path = devicePath,
                    Name = name,
                    IsMouse = false,
                    IsKeyboard = false,
                    BusType = busType,
                    VendorId = vendorId,
                    ProductId = productId,
                    Version = version
                };
            }

            bool isMouse = HasKernelHandler(devicePath, name, procDevicesContent, "mouse") ||
                           CheckIsMouse(fd) ||
                           CheckIsTouchpad(fd);

            bool isKeyboard = CheckIsKeyboard(fd) ||
                              HasKernelHandler(devicePath, name, procDevicesContent, "kbd");

            var device = new InputDevice
            {
                Path = devicePath,
                Name = string.IsNullOrWhiteSpace(name) ? "Unknown Device" : name,
                IsMouse = isMouse,
                IsKeyboard = isKeyboard,
                BusType = busType,
                VendorId = vendorId,
                ProductId = productId,
                Version = version
            };

            // Log device analysis at debug level
            Log.Debug("[InputDeviceHelper] Analyzed: {Path} - {Name} | Type: {Type} | Bus: {Bus} | VID:0x{VID:X4} PID:0x{PID:X4}",
                devicePath, device.Name, device.DeviceType, GetBusTypeName(busType), vendorId, productId);

            return device;
        }
        finally
        {
            EvdevNative.close(fd);
        }
    }

    private static (ushort busType, ushort vendorId, ushort productId, ushort version) ReadDeviceId(int fd)
    {
        // input_id structure: 4 x ushort = 8 bytes
        // struct input_id { __u16 bustype, vendor, product, version; }
        byte[] idBuf = new byte[8];
        int result = EvdevNative.ioctl(fd, EvdevNative.EVIOCGID, idBuf);

        if (result < 0)
        {
            return (0, 0, 0, 0);
        }

        ushort busType = BitConverter.ToUInt16(idBuf, 0);
        ushort vendorId = BitConverter.ToUInt16(idBuf, 2);
        ushort productId = BitConverter.ToUInt16(idBuf, 4);
        ushort version = BitConverter.ToUInt16(idBuf, 6);

        return (busType, vendorId, productId, version);
    }

    private static string GetBusTypeName(ushort busType)
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
            _ => $"Unknown(0x{busType:X2})"
        };
    }

    private static bool ShouldExcludeDevice(string name)
    {
        if (name.Equals("Power Button", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Sleep Button", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Video Bus", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Lid Switch", StringComparison.OrdinalIgnoreCase))
            return true;

        if (name.EndsWith(" Consumer Control", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(" System Control", StringComparison.OrdinalIgnoreCase))
            return true;

        if (name.Contains("WMI", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("hotkeys", StringComparison.OrdinalIgnoreCase))
            return true;

        if (name.Contains("AVRCP", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool HasKernelHandler(string devicePath, string deviceName, string? procContent, string handlerType)
    {
        if (string.IsNullOrEmpty(procContent)) return false;

        var eventName = Path.GetFileName(devicePath);
        var blocks = procContent.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            if (!block.Contains(eventName, StringComparison.Ordinal))
                continue;

            bool nameMatches = false;
            bool hasHandler = false;

            using var reader = new StringReader(block);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("N: Name=") && line.Contains(deviceName))
                    nameMatches = true;

                if (line.StartsWith("H: Handlers=") && line.Contains(eventName))
                {
                    hasHandler = handlerType == "mouse"
                        ? MouseHandlerRegex.IsMatch(line)
                        : line.Contains("kbd", StringComparison.Ordinal);
                }
            }

            if (nameMatches && hasHandler) return true;
        }

        return false;
    }

    private static bool CheckIsMouse(int fd)
    {
        // Must have REL and KEY event types
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_REL) ||
            !HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_KEY))
            return false;

        // Must have REL_X and REL_Y for movement
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_REL, UInputNative.REL_X) ||
            !HasCapability(fd, EvdevNative.EVIOCGBIT_REL, UInputNative.REL_Y))
            return false;

        // Check for any mouse button (BTN_LEFT to BTN_TASK)
        // Gaming mice may not report BTN_LEFT on main pointer interface
        for (int btn = UInputNative.BTN_LEFT; btn <= UInputNative.BTN_TASK; btn++)
        {
            if (HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, btn))
                return true;
        }
        return false;
    }

    private static bool CheckIsTouchpad(int fd)
    {
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_ABS) ||
            !HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_KEY))
            return false;

        bool hasButton = HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, UInputNative.BTN_TOUCH) ||
                         HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, UInputNative.BTN_LEFT);
        if (!hasButton) return false;

        bool hasPosition = (HasCapability(fd, EvdevNative.EVIOCGBIT_ABS, UInputNative.ABS_X) &&
                            HasCapability(fd, EvdevNative.EVIOCGBIT_ABS, UInputNative.ABS_Y)) ||
                           (HasCapability(fd, EvdevNative.EVIOCGBIT_ABS, UInputNative.ABS_MT_POSITION_X) &&
                            HasCapability(fd, EvdevNative.EVIOCGBIT_ABS, UInputNative.ABS_MT_POSITION_Y));
        if (!hasPosition) return false;

        return !HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_REL);
    }

    private static bool CheckIsKeyboard(int fd)
    {
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_KEY))
            return false;

        bool hasEscOrEnter = HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, 1) ||
                             HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, 28);
        if (!hasEscOrEnter) return false;

        for (int keyCode = 30; keyCode <= 44; keyCode++)
        {
            if (HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, keyCode))
                return true;
        }

        return false;
    }

    private static bool HasCapability(int fd, ulong type, int code)
    {
        // Buffer size must accommodate KEY_MAX (0x2FF = 767)
        // Required: (767 / 8) + 1 = 96 bytes minimum
        // Using 96 bytes to cover all possible key codes including gaming mouse buttons
        byte[] mask = new byte[96];
        int len = EvdevNative.ioctl(fd, type, mask);
        if (len < 0) return false;

        int byteIndex = code / 8;
        int bitIndex = code % 8;

        return byteIndex < mask.Length && (mask[byteIndex] & (1 << bitIndex)) != 0;
    }

    public static Dictionary<int, string> GetSupportedKeyCodes(string devicePath)
    {
        var result = new Dictionary<int, string>();

        int fd = EvdevNative.open(devicePath, EvdevNative.O_RDONLY);
        if (fd < 0)
        {
            Log.Warning("Cannot open {Path} for key enumeration", devicePath);
            return result;
        }

        try
        {
            byte[] keyMask = new byte[128];
            int len = EvdevNative.ioctl(fd, EvdevNative.EVIOCGBIT_KEY, keyMask);
            if (len < 0) return result;

            for (int keyCode = 0; keyCode <= LinuxKeyCodeRegistry.KEY_MAX; keyCode++)
            {
                int byteIndex = keyCode / 8;
                int bitIndex = keyCode % 8;

                if (byteIndex < keyMask.Length && (keyMask[byteIndex] & (1 << bitIndex)) != 0)
                    result[keyCode] = LinuxKeyCodeRegistry.GetKeyName(keyCode);
            }
        }
        finally
        {
            EvdevNative.close(fd);
        }

        return result;
    }

    private static bool IsVirtualDevice(string devicePath, string deviceName)
    {
        if (deviceName.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("uinput", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("CrossMacro", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            var eventName = Path.GetFileName(devicePath);
            var sysPath = $"/sys/class/input/{eventName}/device";

            if (Directory.Exists(sysPath))
            {
                var realPath = new DirectoryInfo(sysPath).FullName;
                if (realPath.Contains("/sys/devices/virtual/"))
                    return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool CanOpenForReading(string devicePath)
    {
        int fd = -1;
        try
        {
            fd = EvdevNative.open(devicePath, EvdevNative.O_RDONLY | EvdevNative.O_NONBLOCK);
            if (fd < 0) return false;

            IntPtr bufferPtr = Marshal.AllocHGlobal(24);
            try
            {
                var result = EvdevNative.read(fd, bufferPtr, (IntPtr)24);
                if (result.ToInt32() < 0)
                {
                    int errno = Marshal.GetLastWin32Error();
                    return errno == 11 || errno == 0; // EAGAIN veya başarılı
                }
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(bufferPtr);
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            if (fd >= 0) EvdevNative.close(fd);
        }
    }
}
