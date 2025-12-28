using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using CrossMacro.Platform.Linux.Native.UInput;
using Serilog;

namespace CrossMacro.Platform.Linux.Native.Evdev;

public class InputDeviceHelper
{
    public class InputDevice
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsMouse { get; set; }
        public bool IsKeyboard { get; set; }
        public bool IsVirtual { get; set; }
        
        public override string ToString() => $"{Name} ({Path}) [{(IsMouse ? "Mouse" : "")}{(IsMouse && IsKeyboard ? ", " : "")}{(IsKeyboard ? "Keyboard" : "")}{(IsVirtual ? " (Virtual)" : "")}]";
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
            var procDevices = "/proc/bus/input/devices";
            if (File.Exists(procDevices))
            {
                procDevicesContent = File.ReadAllText(procDevices);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to read /proc/bus/input/devices, some device detection may be less accurate");
        }

        foreach (var file in files)
        {
            try
            {
                var device = GetDeviceInfo(file, procDevicesContent);
                
                if ((device.IsMouse || device.IsKeyboard) && CanOpenForReading(file))
                {
                    Log.Information("Device found: {Name} (Mouse: {IsMouse}, Keyboard: {IsKeyboard}) - Added", device.Name, device.IsMouse, device.IsKeyboard);
                    devices.Add(device);
                }
                else
                {
                    if (device.IsVirtual)
                    {
                        Log.Information("SKIPPING VIRTUAL DEVICE: {Name} ({Path})", device.Name, device.Path);
                    }
                    else
                    {
                        Log.Debug("Device found: {Name} - Skipped (Not relevant or permission denied)", device.Name);
                    }
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
            throw new IOException($"Cannot open {devicePath}. Errno: {errno}. Check permissions.");
        }

        try
        {
            byte[] nameBuf = new byte[256];
            EvdevNative.ioctl(fd, EvdevNative.EVIOCGNAME_256, nameBuf);
            string name = System.Text.Encoding.ASCII.GetString(nameBuf).TrimEnd('\0');

            if (IsVirtualDevice(devicePath, name))
            {
                Log.Debug("[InputDeviceHelper] Excluding '{Name}' at {Path} - virtual device", name, devicePath);
                return new InputDevice
                {
                    Path = devicePath,
                    Name = name,
                    IsMouse = false,
                    IsKeyboard = false,
                    IsVirtual = true
                };
            }

            bool isMouse = CheckIsMouse(fd);
            bool isKeyboard = CheckIsKeyboard(fd);
            bool isTouchpad = false;
            
            if (!isMouse)
            {
                isTouchpad = CheckIsTouchpad(fd);
                if (isTouchpad)
                {
                    isMouse = true;
                    Log.Debug("[InputDeviceHelper] Device '{Name}' detected as touchpad, treating as mouse", name);
                }
            }
            
            if (!isMouse)
            {
                isMouse = IsMouseFromProcDevices(devicePath, name, procDevicesContent);
            }
            
            if (isKeyboard && !isMouse && name.Contains(" Keyboard"))
            {
                if (BelongsToMouseDevice(name, procDevicesContent))
                {
                    Log.Debug("[InputDeviceHelper] Excluding '{Name}' - keyboard interface of a mouse device", name);
                    isKeyboard = false;
                }
            }
            
            if (isMouse && !isKeyboard && name.Contains(" Mouse"))
            {
                if (BelongsToKeyboardDevice(name, procDevicesContent))
                {
                    Log.Debug("[InputDeviceHelper] Excluding '{Name}' - mouse interface of a keyboard device", name);
                    isMouse = false;
                }
            }

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

    private static bool CheckIsMouse(int fd)
    {
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_REL))
            return false;

        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_KEY))
            return false;

        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, UInputNative.BTN_LEFT))
            return false;

        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_REL, UInputNative.REL_X))
            return false;

        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_REL, UInputNative.REL_Y))
            return false;

        return true;
    }

    private static bool CheckIsTouchpad(int fd)
    {
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_ABS))
            return false;

        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_KEY))
            return false;

        bool hasTouchBtn = HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, UInputNative.BTN_TOUCH);
        bool hasLeftBtn = HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, UInputNative.BTN_LEFT);
        
        if (!hasTouchBtn && !hasLeftBtn)
            return false;

        bool hasAbsX = HasCapability(fd, EvdevNative.EVIOCGBIT_ABS, UInputNative.ABS_X);
        bool hasAbsY = HasCapability(fd, EvdevNative.EVIOCGBIT_ABS, UInputNative.ABS_Y);
        
        bool hasMtX = HasCapability(fd, EvdevNative.EVIOCGBIT_ABS, UInputNative.ABS_MT_POSITION_X);
        bool hasMtY = HasCapability(fd, EvdevNative.EVIOCGBIT_ABS, UInputNative.ABS_MT_POSITION_Y);
        
        if (!((hasAbsX && hasAbsY) || (hasMtX && hasMtY)))
            return false;

        if (HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_REL))
        {
            return false;
        }

        return true;
    }

    private static bool CheckIsKeyboard(int fd)
    {
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_KEY))
            return false;

        bool hasEsc = HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, 1); 
        bool hasEnter = HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, 28); 
        
        if (!hasEsc && !hasEnter)
            return false;
        
        bool hasLetterKey = false;
        for (int keyCode = 30; keyCode <= 44; keyCode++)
        {
            if (HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, keyCode))
            {
                hasLetterKey = true;
                break;
            }
        }
        
        if (!hasLetterKey)
            return false;

        return true;
    }

    private static bool HasCapability(int fd, ulong type, int code)
    {
        byte[] mask = new byte[64]; 
        int len = EvdevNative.ioctl(fd, type, mask);
        
        if (len < 0)
            return false;

        int byteIndex = code / 8;
        int bitIndex = code % 8;

        if (byteIndex >= mask.Length)
            return false;

        return (mask[byteIndex] & (1 << bitIndex)) != 0;
    }

    private static bool IsMouseFromProcDevices(string devicePath, string deviceName, string? procDevicesContent)
    {
        try
        {
            if (string.IsNullOrEmpty(procDevicesContent))
                return false;

            var eventName = Path.GetFileName(devicePath);

            var devices = procDevicesContent.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var device in devices)
            {
                var lines = device.Split('\n');
                bool nameMatches = false;
                bool hasMouseHandler = false;
                bool eventMatches = false;

                foreach (var line in lines)
                {
                    if (line.StartsWith("N: Name=") && line.Contains(deviceName))
                    {
                        nameMatches = true;
                    }
                    
                    if (line.StartsWith("H: Handlers="))
                    {
                        if (line.Contains(eventName) && 
                            System.Text.RegularExpressions.Regex.IsMatch(line, @"\bmouse\d+\b"))
                        {
                            hasMouseHandler = true;
                            eventMatches = true;
                        }
                    }
                }

                if (nameMatches && hasMouseHandler && eventMatches)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[InputDeviceHelper] Failed to read /proc/bus/input/devices");
        }

        return false;
    }

    private static bool BelongsToMouseDevice(string deviceName, string? procDevicesContent)
    {
        try
        {
            if (string.IsNullOrEmpty(procDevicesContent))
                return false;

            var devices = procDevicesContent.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            var baseName = deviceName.Replace(" Keyboard", "").Trim();

            foreach (var device in devices)
            {
                var lines = device.Split('\n');
                
                bool nameMatches = false;
                bool hasMouseHandler = false;

                foreach (var line in lines)
                {
                    if (line.StartsWith("N: Name=") && line.Contains($"\"{baseName}\""))
                    {
                        nameMatches = true;
                    }
                    
                    if (line.StartsWith("H: Handlers=") && 
                        System.Text.RegularExpressions.Regex.IsMatch(line, @"\bmouse\d+\b"))
                    {
                        hasMouseHandler = true;
                    }
                }

                if (nameMatches && hasMouseHandler)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[InputDeviceHelper] Failed to check mouse device");
        }

        return false;
    }

    private static bool BelongsToKeyboardDevice(string deviceName, string? procDevicesContent)
    {
        try
        {
            if (string.IsNullOrEmpty(procDevicesContent))
                return false;

            var devices = procDevicesContent.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            var baseName = deviceName.Replace(" Mouse", "").Trim();

            foreach (var device in devices)
            {
                var lines = device.Split('\n');
                
                bool nameMatches = false;
                bool hasKbdHandler = false;

                foreach (var line in lines)
                {
                    if (line.StartsWith("N: Name=") && line.Contains($"\"{baseName}\""))
                    {
                        nameMatches = true;
                    }
                    
                    if (line.StartsWith("H: Handlers=") && line.Contains("kbd"))
                    {
                        hasKbdHandler = true;
                    }
                }

                if (nameMatches && hasKbdHandler)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[InputDeviceHelper] Failed to check keyboard device");
        }

        return false;
    }

    private static bool IsVirtualDevice(string devicePath, string deviceName)
    {
        if (deviceName.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("uinput", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("CrossMacro", StringComparison.OrdinalIgnoreCase))
        {
            Log.Debug("[InputDeviceHelper] Device '{Name}' identified as virtual by name pattern", deviceName);
            return true;
        }

        try
        {
            var eventName = Path.GetFileName(devicePath); 
            var sysPath = $"/sys/class/input/{eventName}/device";
            
            if (Directory.Exists(sysPath))
            {
                var realPath = new DirectoryInfo(sysPath).FullName;
                
                if (realPath.Contains("/sys/devices/virtual/"))
                {
                    Log.Debug("[InputDeviceHelper] Device '{Name}' at {Path} identified as virtual by sysfs path: {SysPath}", 
                        deviceName, devicePath, realPath);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[InputDeviceHelper] Failed to check sysfs path for {Path}", devicePath);
        }

        return false;
    }

    private static bool CanOpenForReading(string devicePath)
    {
        int testFd = -1;
        try
        {
            testFd = EvdevNative.open(devicePath, EvdevNative.O_RDONLY | EvdevNative.O_NONBLOCK);
            if (testFd < 0)
            {
                return false;
            }

            byte[] testBuffer = new byte[24];
            IntPtr bufferPtr = Marshal.AllocHGlobal(testBuffer.Length);
            try
            {
                var result = EvdevNative.read(testFd, bufferPtr, (IntPtr)testBuffer.Length);
                
                if (result.ToInt32() < 0)
                {
                    int errno = Marshal.GetLastWin32Error();
                    if (errno != 11 && errno != 0) 
                    {
                        return false;
                    }
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
            if (testFd >= 0)
            {
                EvdevNative.close(testFd);
            }
        }
    }
}
