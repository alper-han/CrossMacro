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

        public override string ToString() =>
            $"{Name} ({Path}) [{(IsMouse ? "Mouse" : "")}{(IsMouse && IsKeyboard ? ", " : "")}{(IsKeyboard ? "Keyboard" : "")}{(IsVirtual ? " (Virtual)" : "")}]";
    }

    public static List<InputDevice> GetAvailableDevices()
    {
        List<InputDevice> devices = [];
        var inputDir = "/dev/input";

        Log.Information("Scanning input devices in {InputDir}...", inputDir);

        if (!Directory.Exists(inputDir))
        {
            Log.Warning("Directory {InputDir} does not exist.", inputDir);
            return devices;
        }

        var files = Directory.GetFiles(inputDir, "event*");
        Log.Information("Found {Count} event files.", files.Length);

        string? procDevicesContent = null;
        try
        {
            if (File.Exists("/proc/bus/input/devices"))
                procDevicesContent = File.ReadAllText("/proc/bus/input/devices");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read /proc/bus/input/devices");
        }

        foreach (var file in files)
        {
            try
            {
                var device = GetDeviceInfo(file, procDevicesContent);

                if ((device.IsMouse || device.IsKeyboard) && CanOpenForReading(file))
                {
                    Log.Information("Device found: {Name} (Mouse: {IsMouse}, Keyboard: {IsKeyboard})",
                        device.Name, device.IsMouse, device.IsKeyboard);
                    devices.Add(device);
                }
                else if (device.IsVirtual)
                {
                    Log.Debug("Skipping virtual device: {Name}", device.Name);
                }
                else
                {
                    Log.Debug("Skipping device: {Name}", device.Name);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading {File}", file);
            }
        }

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

            if (IsVirtualDevice(devicePath, name))
            {
                return new InputDevice
                {
                    Path = devicePath,
                    Name = name,
                    IsMouse = false,
                    IsKeyboard = false,
                    IsVirtual = true
                };
            }

            if (ShouldExcludeDevice(name))
            {
                Log.Debug("Excluding system/auxiliary device: {Name}", name);
                return new InputDevice
                {
                    Path = devicePath,
                    Name = name,
                    IsMouse = false,
                    IsKeyboard = false
                };
            }

            bool isMouse = HasKernelHandler(devicePath, name, procDevicesContent, "mouse") ||
                           CheckIsMouse(fd) ||
                           CheckIsTouchpad(fd);

            bool isKeyboard = CheckIsKeyboard(fd) ||
                              HasKernelHandler(devicePath, name, procDevicesContent, "kbd");

            return new InputDevice
            {
                Path = devicePath,
                Name = string.IsNullOrWhiteSpace(name) ? "Unknown Device" : name,
                IsMouse = isMouse,
                IsKeyboard = isKeyboard
            };
        }
        finally
        {
            EvdevNative.close(fd);
        }
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
        byte[] mask = new byte[64];
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
