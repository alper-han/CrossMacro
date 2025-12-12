using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using CrossMacro.Native.UInput;
using Serilog;

namespace CrossMacro.Native.Evdev;

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
        var devices = new List<InputDevice>();
        var inputDir = "/dev/input";

        Log.Information("Scanning input devices in {InputDir}...", inputDir);

        if (!Directory.Exists(inputDir))
        {
            Log.Warning("Directory {InputDir} does not exist.", inputDir);
            return devices;
        }

        var files = Directory.GetFiles(inputDir, "event*");
        Log.Information("Found {Count} event files.", files.Length);

        // Cache /proc/bus/input/devices content to avoid reading it multiple times
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
                
                // Include devices that are mice OR keyboards AND can be opened for reading
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
            // Get Name
            byte[] nameBuf = new byte[256];
            EvdevNative.ioctl(fd, EvdevNative.EVIOCGNAME_256, nameBuf);
            string name = System.Text.Encoding.ASCII.GetString(nameBuf).TrimEnd('\0');

            // CRITICAL: Filter out virtual devices (uinput, virtual mice created by MacroPlayer)
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

            // Check capabilities
            bool isMouse = CheckIsMouse(fd);
            bool isKeyboard = CheckIsKeyboard(fd);
            bool isTouchpad = false;
            
            // If not detected as a mouse, check if it's a touchpad
            // Touchpads use EV_ABS instead of EV_REL
            if (!isMouse)
            {
                isTouchpad = CheckIsTouchpad(fd);
                if (isTouchpad)
                {
                    // Treat touchpad as a mouse for recording purposes
                    isMouse = true;
                    Log.Debug("[InputDeviceHelper] Device '{Name}' detected as touchpad, treating as mouse", name);
                }
            }
            
            // Fallback: Check /proc/bus/input/devices for "mouseX" handler
            if (!isMouse)
            {
                isMouse = IsMouseFromProcDevices(devicePath, name, procDevicesContent);
            }
            
            // IMPORTANT: Filter out "keyboard" interfaces that belong to mice
            // Many gaming mice expose a "Keyboard" interface for macro keys.
            // Only exclude if the device name explicitly contains "Keyboard" as a suffix
            if (isKeyboard && !isMouse && name.Contains(" Keyboard"))
            {
                if (BelongsToMouseDevice(name, procDevicesContent))
                {
                    Log.Debug("[InputDeviceHelper] Excluding '{Name}' - keyboard interface of a mouse device", name);
                    isKeyboard = false;
                }
            }
            
            // Similarly, filter out "mouse" interfaces that belong to keyboards
            // Some keyboards have volume wheels that appear as mice
            // Only exclude if the device name explicitly contains "Mouse" as a suffix
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
        // Check for EV_REL (Relative movement)
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_REL))
            return false;

        // Check for EV_KEY (Keys/Buttons)
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_KEY))
            return false;

        // Check for BTN_LEFT (primary button)
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, UInputNative.BTN_LEFT))
            return false;

        // Check for REL_X and REL_Y (mouse movement axes)
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_REL, UInputNative.REL_X))
            return false;

        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_REL, UInputNative.REL_Y))
            return false;

        return true;
    }

    /// <summary>
    /// Check if this is a touchpad device
    /// Touchpads use absolute positioning (EV_ABS) instead of relative movement (EV_REL)
    /// </summary>
    private static bool CheckIsTouchpad(int fd)
    {
        // Check for EV_ABS (Absolute positioning - touchpads use this)
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_ABS))
            return false;

        // Check for EV_KEY (for tap/click events)
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_KEY))
            return false;

        // Check for BTN_TOUCH or BTN_LEFT (touchpad buttons)
        bool hasTouchBtn = HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, UInputNative.BTN_TOUCH);
        bool hasLeftBtn = HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, UInputNative.BTN_LEFT);
        
        if (!hasTouchBtn && !hasLeftBtn)
            return false;

        // Check for ABS_X and ABS_Y (basic positioning)
        bool hasAbsX = HasCapability(fd, EvdevNative.EVIOCGBIT_ABS, UInputNative.ABS_X);
        bool hasAbsY = HasCapability(fd, EvdevNative.EVIOCGBIT_ABS, UInputNative.ABS_Y);
        
        // Check for multitouch axes (more reliable for touchpads)
        bool hasMtX = HasCapability(fd, EvdevNative.EVIOCGBIT_ABS, UInputNative.ABS_MT_POSITION_X);
        bool hasMtY = HasCapability(fd, EvdevNative.EVIOCGBIT_ABS, UInputNative.ABS_MT_POSITION_Y);
        
        // Must have either basic ABS or multitouch axes
        if (!((hasAbsX && hasAbsY) || (hasMtX && hasMtY)))
            return false;

        // Make sure it's NOT a regular mouse (mice have EV_REL)
        if (HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_REL))
        {
            // Has relative movement - this is a mouse, not a touchpad
            return false;
        }

        return true;
    }

    private static bool CheckIsKeyboard(int fd)
    {
        // Check for EV_KEY
        if (!HasCapability(fd, EvdevNative.EVIOCGBIT_EV, UInputNative.EV_KEY))
            return false;

        bool hasEsc = HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, 1); 
        bool hasEnter = HasCapability(fd, EvdevNative.EVIOCGBIT_KEY, 28); 
        
        if (!hasEsc && !hasEnter)
            return false;
        
        // Check for at least one letter key (A-Z range is 30-44)
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

    /// <summary>
    /// Check /proc/bus/input/devices to see if this specific event device has a "mouseX" handler
    /// This is more reliable than capability checks for some wireless mice
    /// </summary>
    private static bool IsMouseFromProcDevices(string devicePath, string deviceName, string? procDevicesContent)
    {
        try
        {
            if (string.IsNullOrEmpty(procDevicesContent))
                return false;

            // Extract event number from path (e.g., /dev/input/event8 -> "event8")
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
                    // Check if name matches
                    if (line.StartsWith("N: Name=") && line.Contains(deviceName))
                    {
                        nameMatches = true;
                    }
                    
                    // Check if this specific event has mouseX handler
                    if (line.StartsWith("H: Handlers="))
                    {
                        // Check if line contains both our event AND a mouseX handler
                        if (line.Contains(eventName) && 
                            System.Text.RegularExpressions.Regex.IsMatch(line, @"\bmouse\d+\b"))
                        {
                            hasMouseHandler = true;
                            eventMatches = true;
                        }
                    }
                }

                // Only return true if name matches AND this specific event has mouseX handler
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

    /// <summary>
    /// Check if this device belongs to a physical device that also has a mouse interface
    /// This helps filter out "Keyboard" interfaces on gaming mice
    /// </summary>
    private static bool BelongsToMouseDevice(string deviceName, string? procDevicesContent)
    {
        try
        {
            if (string.IsNullOrEmpty(procDevicesContent))
                return false;

            var devices = procDevicesContent.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            // Extract the base device name (remove the " Keyboard" suffix)
            var baseName = deviceName.Replace(" Keyboard", "").Trim();

            // Look for a device with the base name that has a mouse handler
            foreach (var device in devices)
            {
                var lines = device.Split('\n');
                
                bool nameMatches = false;
                bool hasMouseHandler = false;

                foreach (var line in lines)
                {
                    // Check for exact base name match
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

                // If we found the base device with a mouse handler, this keyboard interface belongs to a mouse
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

    /// <summary>
    /// Check if this device belongs to a physical device that also has a keyboard interface
    /// This helps filter out "Mouse" interfaces on keyboards (e.g., volume wheels)
    /// </summary>
    private static bool BelongsToKeyboardDevice(string deviceName, string? procDevicesContent)
    {
        try
        {
            if (string.IsNullOrEmpty(procDevicesContent))
                return false;

            var devices = procDevicesContent.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            // Extract the base device name (remove the " Mouse" suffix)
            var baseName = deviceName.Replace(" Mouse", "").Trim();

            // Look for a device with the base name that has a kbd handler
            foreach (var device in devices)
            {
                var lines = device.Split('\n');
                
                bool nameMatches = false;
                bool hasKbdHandler = false;

                foreach (var line in lines)
                {
                    // Check for exact base name match
                    if (line.StartsWith("N: Name=") && line.Contains($"\"{baseName}\""))
                    {
                        nameMatches = true;
                    }
                    
                    if (line.StartsWith("H: Handlers=") && line.Contains("kbd"))
                    {
                        hasKbdHandler = true;
                    }
                }

                // If we found the base device with a kbd handler, this mouse interface belongs to a keyboard
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

    /// <summary>
    /// Check if a device is a virtual device (uinput, virtual mouse/keyboard)
    /// This prevents capturing from devices created by MacroPlayer or other virtual input tools
    /// </summary>
    private static bool IsVirtualDevice(string devicePath, string deviceName)
    {
        // Filter by device name patterns
        if (deviceName.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("uinput", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("CrossMacro", StringComparison.OrdinalIgnoreCase))
        {
            Log.Debug("[InputDeviceHelper] Device '{Name}' identified as virtual by name pattern", deviceName);
            return true;
        }

        // Check if device is under /sys/devices/virtual/
        try
        {
            var eventName = Path.GetFileName(devicePath); // e.g., "event8"
            var sysPath = $"/sys/class/input/{eventName}/device";
            
            if (Directory.Exists(sysPath))
            {
                // Get the real path (follows symlinks)
                var realPath = new DirectoryInfo(sysPath).FullName;
                
                // Check if it's under /sys/devices/virtual/
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

    /// <summary>
    /// Test if we can actually open the device for reading (permission check)
    /// Also verifies that the device is not grabbed by another process
    /// </summary>
    private static bool CanOpenForReading(string devicePath)
    {
        int testFd = -1;
        try
        {
            // Try to open with O_NONBLOCK to avoid blocking
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
