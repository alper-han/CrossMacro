
namespace CrossMacro.Platform.Linux.Native.Evdev;

public static class InputDeviceHelper
{
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

        public string DeviceType => InputDeviceClassification.GetDeviceType(IsVirtual, IsMouse, IsKeyboard);

        public override string ToString() =>
            $"{Name} ({Path}) [{DeviceType}] VID:0x{VendorId:X4} PID:0x{ProductId:X4}";
    }

    public static IReadOnlyList<InputDevice> GetAvailableDevices()
    {
        return GetAvailableDevices(logSummary: true);
    }

    public static IReadOnlyList<InputDevice> GetAvailableDevices(bool logSummary, bool logInaccessibleWarning = true)
    {
        List<InputDevice> devices = [];
        List<InputDevice> skippedDevices = [];
        List<(InputDevice device, int errno)> inaccessibleDevices = [];
        var readErrors = 0;
        const string inputDir = "/dev/input";

        if (logSummary)
        {
            Log.Information("[InputDeviceHelper] Scanning input devices in {InputDir}...", inputDir);
        }

        if (!Directory.Exists(inputDir))
        {
            Log.Warning("[InputDeviceHelper] Directory {InputDir} does not exist.", inputDir);
            return devices;
        }

        var files = Directory.GetFiles(inputDir, "event*");
        Log.Debug("[InputDeviceHelper] Found {Count} event files to analyze.", files.Length);

        var procDevicesContent = ReadProcDevicesContent();
        ScanDeviceFiles(files, procDevicesContent, devices, skippedDevices, inaccessibleDevices, ref readErrors);

        if (logSummary)
        {
            LogDeviceSummary(files.Length, devices, inaccessibleDevices, skippedDevices, readErrors, logInaccessibleWarning);
        }

        return devices;
    }

    public static Task<IReadOnlyList<InputDevice>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
    {
        return GetAvailableDevicesAsync(logInaccessibleWarning: true, cancellationToken);
    }

    public static async Task<IReadOnlyList<InputDevice>> GetAvailableDevicesAsync(
        bool logInaccessibleWarning,
        CancellationToken cancellationToken = default)
    {
        List<InputDevice> devices = [];
        List<InputDevice> skippedDevices = [];
        List<(InputDevice device, int errno)> inaccessibleDevices = [];
        var readErrors = 0;
        const string inputDir = "/dev/input";

        Log.Information("[InputDeviceHelper] Scanning input devices in {InputDir}...", inputDir);

        if (!Directory.Exists(inputDir))
        {
            Log.Warning("[InputDeviceHelper] Directory {InputDir} does not exist.", inputDir);
            return devices;
        }

        var files = Directory.GetFiles(inputDir, "event*");
        Log.Debug("[InputDeviceHelper] Found {Count} event files to analyze.", files.Length);

        var procDevicesContent = await ReadProcDevicesContentAsync(cancellationToken).ConfigureAwait(false);
        ScanDeviceFiles(files, procDevicesContent, devices, skippedDevices, inaccessibleDevices, ref readErrors);

        LogDeviceSummary(files.Length, devices, inaccessibleDevices, skippedDevices, readErrors, logInaccessibleWarning);

        return devices;
    }

    private static void LogDeviceSummary(
        int fileCount,
        List<InputDevice> devices,
        List<(InputDevice device, int errno)> inaccessibleDevices,
        List<InputDevice> skippedDevices,
        int readErrors,
        bool logInaccessibleWarning)
    {
        Log.Information("[InputDeviceHelper] ========== Device Summary ==========");
        Log.Information("[InputDeviceHelper] Total: {Total} | Usable: {Usable} | Inaccessible: {Inaccessible} | Skipped: {Skipped} | ReadErrors: {ReadErrors}",
            fileCount, devices.Count, inaccessibleDevices.Count, skippedDevices.Count, readErrors);

        if (devices.Count > 0)
        {
            Log.Information("[InputDeviceHelper] --- Active Input Devices ---");
            foreach (var dev in devices)
            {
                Log.Information("[InputDeviceHelper]   [{Type}] {Name} ({Path}) | Bus: {Bus} | VID:0x{VID:X4} PID:0x{PID:X4}",
                    dev.DeviceType, dev.Name, dev.Path, InputDeviceClassification.GetBusTypeName(dev.BusType), dev.VendorId, dev.ProductId);
            }
        }

        LogInaccessibleDevices(inaccessibleDevices, logInaccessibleWarning);
        LogSkippedDevices(skippedDevices);
        Log.Information("[InputDeviceHelper] ====================================");
    }

    private static void LogInaccessibleDevices(
        List<(InputDevice device, int errno)> inaccessibleDevices,
        bool logWarning)
    {
        if (inaccessibleDevices.Count is 0)
        {
            return;
        }

        if (logWarning)
        {
            Log.Warning(
                "[InputDeviceHelper] {Count} input device(s) are inaccessible; direct evdev access may be unavailable. Detailed device entries are available at Debug level.",
                inaccessibleDevices.Count);
        }
        foreach (var (dev, errno) in inaccessibleDevices)
        {
            if (errno is 16)
            {
                Log.Debug("[InputDeviceHelper]   [{Type}] {Name} ({Path}) - Device is exclusively grabbed. Run: sudo fuser -v {Path}",
                    dev.DeviceType, dev.Name, dev.Path, dev.Path);
            }
            else
            {
                Log.Debug("[InputDeviceHelper]   [{Type}] {Name} ({Path}) | VID:0x{VID:X4} PID:0x{PID:X4} - Cannot open (errno: {Errno})",
                    dev.DeviceType, dev.Name, dev.Path, dev.VendorId, dev.ProductId, errno);
            }
        }
    }

    private static void LogSkippedDevices(List<InputDevice> skippedDevices)
    {
        if (skippedDevices.Count is 0)
        {
            return;
        }

        Log.Debug("[InputDeviceHelper] --- Skipped Devices (not input devices) ---");
        foreach (var dev in skippedDevices)
        {
            Log.Debug("[InputDeviceHelper]   [{Type}] {Name} ({Path}) | VID:0x{VID:X4} PID:0x{PID:X4}",
                dev.DeviceType, dev.Name, dev.Path, dev.VendorId, dev.ProductId);
        }
    }

    private static string? ReadProcDevicesContent()
    {
        try
        {
            return File.Exists("/proc/bus/input/devices")
                ? File.ReadAllText("/proc/bus/input/devices")
                : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[InputDeviceHelper] Failed to read /proc/bus/input/devices");
            return null;
        }
    }

    private static async Task<string?> ReadProcDevicesContentAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists("/proc/bus/input/devices"))
            {
                return null;
            }

            return await File.ReadAllTextAsync("/proc/bus/input/devices", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[InputDeviceHelper] Failed to read /proc/bus/input/devices");
            return null;
        }
    }

    private static void ScanDeviceFiles(
        string[] files,
        string? procDevicesContent,
        List<InputDevice> devices,
        List<InputDevice> skippedDevices,
        List<(InputDevice device, int errno)> inaccessibleDevices,
        ref int readErrors)
    {
        foreach (var file in files)
        {
            try
            {
                var device = GetDeviceInfo(file, procDevicesContent);
                if (device.IsMouse || device.IsKeyboard)
                {
                    var (canOpen, errno) = CanOpenForReading(file);
                    if (canOpen)
                    {
                        devices.Add(device);
                    }
                    else
                    {
                        inaccessibleDevices.Add((device, errno));
                    }
                }
                else
                {
                    skippedDevices.Add(device);
                }
            }
            catch (DeviceOpenException ex) when (ex.Errno is 13 or 16)
            {
                inaccessibleDevices.Add((CreateInaccessiblePlaceholder(file), ex.Errno));
            }
            catch (DeviceOpenException ex) when (ex.Errno is 2)
            {
                Log.Debug("[InputDeviceHelper] Device file {File} disappeared before it could be opened (race condition).", file);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                readErrors++;
                Log.LogError(ex, "[InputDeviceHelper] Error reading {File}", file);
            }
        }
    }

    private static InputDevice GetDeviceInfo(string devicePath, string? procDevicesContent)
    {
        int fd = EvdevNative.open(devicePath, EvdevNative.O_RDONLY);
        if (fd < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            throw new DeviceOpenException(devicePath, errno);
        }

        try
        {
            byte[] nameBuf = new byte[256];
            _ = EvdevNative.ioctl(fd, EvdevNative.EVIOCGNAME_256, nameBuf);
            string name = System.Text.Encoding.ASCII.GetString(nameBuf).TrimEnd('\0');

            var (busType, vendorId, productId, version) = ReadDeviceId(fd);

            if (VirtualDeviceConstants.IsCrossMacroVirtualDevice(name, vendorId, productId))
            {
                return BuildVirtualInputDevice(devicePath, name, busType, vendorId, productId, version);
            }

            bool isVirtual = IsVirtualDevice(devicePath, name);

            if (InputDeviceClassification.ShouldExclude(name))
            {
                return BuildExcludedInputDevice(devicePath, name, isVirtual, busType, vendorId, productId, version);
            }

            bool isMouse = InputDeviceClassification.HasKernelHandler(devicePath, name, procDevicesContent, "mouse") ||
                           CheckIsMouse(fd) ||
                           CheckIsTouchpad(fd);

            bool isKeyboard = CheckIsKeyboard(fd) ||
                              InputDeviceClassification.HasKernelHandler(devicePath, name, procDevicesContent, "kbd");

            return BuildAnalyzedInputDevice(devicePath, name, isVirtual, isMouse, isKeyboard, busType, vendorId, productId, version);
        }
        finally
        {
            _ = EvdevNative.close(fd);
        }
    }

    private static InputDevice BuildVirtualInputDevice(string devicePath, string name, ushort busType, ushort vendorId, ushort productId, ushort version)
    {
        Log.Debug("[InputDeviceHelper] CrossMacro virtual output device: {Path} - {Name} (VID:0x{VID:X4} PID:0x{PID:X4})",
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
            Version = version,
        };
    }

    private static InputDevice BuildExcludedInputDevice(string devicePath, string name, bool isVirtual, ushort busType, ushort vendorId, ushort productId, ushort version)
    {
        Log.Debug("[InputDeviceHelper] Excluded device: {Path} - {Name} (VID:0x{VID:X4} PID:0x{PID:X4})",
            devicePath, name, vendorId, productId);
        return new InputDevice
        {
            Path = devicePath,
            Name = name,
            IsMouse = false,
            IsKeyboard = false,
            IsVirtual = isVirtual,
            BusType = busType,
            VendorId = vendorId,
            ProductId = productId,
            Version = version,
        };
    }

    private static InputDevice BuildAnalyzedInputDevice(string devicePath, string name, bool isVirtual, bool isMouse, bool isKeyboard, ushort busType, ushort vendorId, ushort productId, ushort version)
    {
        var device = new InputDevice
        {
            Path = devicePath,
            Name = string.IsNullOrWhiteSpace(name) ? "Unknown Device" : name,
            IsMouse = isMouse,
            IsKeyboard = isKeyboard,
            IsVirtual = isVirtual,
            BusType = busType,
            VendorId = vendorId,
            ProductId = productId,
            Version = version,
        };

        Log.Debug("[InputDeviceHelper] Analyzed: {Path} - {Name} | Type: {Type} | Bus: {Bus} | VID:0x{VID:X4} PID:0x{PID:X4}",
            devicePath, device.Name, device.DeviceType, InputDeviceClassification.GetBusTypeName(busType), vendorId, productId);

        return device;
    }

    private static (ushort busType, ushort vendorId, ushort productId, ushort version) ReadDeviceId(int fd)
    {
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

    private static bool CheckIsMouse(int fd)
    {
        if (!HasCapability(fd, UInputNative.EV_SYN, UInputNative.EV_REL) ||
            !HasCapability(fd, UInputNative.EV_SYN, UInputNative.EV_KEY))
        {
            return false;
        }

        if (!HasCapability(fd, UInputNative.EV_REL, UInputNative.REL_X) ||
            !HasCapability(fd, UInputNative.EV_REL, UInputNative.REL_Y))
        {
            return false;
        }

        for (int btn = UInputNative.BTN_LEFT; btn <= UInputNative.BTN_TASK; btn++)
        {
            if (HasCapability(fd, UInputNative.EV_KEY, btn))
            {
                return true;
            }
        }
        return false;
    }

    private static bool CheckIsTouchpad(int fd)
    {
        if (!HasCapability(fd, UInputNative.EV_SYN, UInputNative.EV_ABS) ||
            !HasCapability(fd, UInputNative.EV_SYN, UInputNative.EV_KEY))
        {
            return false;
        }

        bool hasButton = HasCapability(fd, UInputNative.EV_KEY, UInputNative.BTN_TOUCH) ||
                         HasCapability(fd, UInputNative.EV_KEY, UInputNative.BTN_LEFT);
        if (!hasButton)
        {
            return false;
        }

        bool hasPosition = (HasCapability(fd, UInputNative.EV_ABS, UInputNative.ABS_X) &&
                            HasCapability(fd, UInputNative.EV_ABS, UInputNative.ABS_Y)) ||
                           (HasCapability(fd, UInputNative.EV_ABS, UInputNative.ABS_MT_POSITION_X) &&
                            HasCapability(fd, UInputNative.EV_ABS, UInputNative.ABS_MT_POSITION_Y));
        if (!hasPosition)
        {
            return false;
        }

        return !HasCapability(fd, UInputNative.EV_SYN, UInputNative.EV_REL);
    }

    private static bool CheckIsKeyboard(int fd)
    {
        if (!HasCapability(fd, UInputNative.EV_SYN, UInputNative.EV_KEY))
        {
            return false;
        }

        bool hasEscOrEnter = HasCapability(fd, UInputNative.EV_KEY, 1) ||
                             HasCapability(fd, UInputNative.EV_KEY, 28);
        if (!hasEscOrEnter)
        {
            return false;
        }

        for (int keyCode = 30; keyCode <= 44; keyCode++)
        {
            if (HasCapability(fd, UInputNative.EV_KEY, keyCode))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCapability(int fd, int eventType, int code)
    {
        byte[] mask = new byte[96];
        int len = EvdevNative.ioctl(fd, EvdevNative.EVIOCGBIT(eventType, mask.Length), mask);
        if (len < 0)
        {
            return false;
        }

        int byteIndex = code / 8;
        int bitIndex = code % 8;

        return byteIndex < mask.Length && (mask[byteIndex] & (1 << bitIndex)) is not 0;
    }

    public static IReadOnlyDictionary<int, string> GetSupportedKeyCodes(string devicePath)
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
            int len = EvdevNative.ioctl(fd, EvdevNative.EVIOCGBIT(UInputNative.EV_KEY, keyMask.Length), keyMask);
            if (len < 0)
            {
                return result;
            }

            for (int keyCode = 0; keyCode <= 767; keyCode++)
            {
                int byteIndex = keyCode / 8;
                int bitIndex = keyCode % 8;

                if (byteIndex < keyMask.Length && (keyMask[byteIndex] & (1 << bitIndex)) is not 0)
                {
                    result[keyCode] = $"KEY_{keyCode.ToString(CultureInfo.InvariantCulture)}";
                }
            }
        }
        finally
        {
            _ = EvdevNative.close(fd);
        }

        return result;
    }

    private static bool IsVirtualDevice(string devicePath, string deviceName)
    {
        if (deviceName.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("uinput", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("CrossMacro", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            var eventName = Path.GetFileName(devicePath);
            var sysPath = $"/sys/class/input/{eventName}/device";

            if (Directory.Exists(sysPath))
            {
                var realPath = new DirectoryInfo(sysPath).FullName;
                if (realPath.Contains("/sys/devices/virtual/", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Ignore directory check failures; fallback to false.
        }

        return false;
    }

    private static InputDevice CreateInaccessiblePlaceholder(string devicePath)
    {
        return new InputDevice
        {
            Path = devicePath,
            Name = Path.GetFileName(devicePath),
            IsMouse = false,
            IsKeyboard = false,
            IsVirtual = false,
            VendorId = 0,
            ProductId = 0,
            BusType = 0,
            Version = 0,
        };
    }

    private static (bool canOpen, int errno) CanOpenForReading(string devicePath)
    {
        int fd = -1;
        try
        {
            fd = EvdevNative.open(devicePath, EvdevNative.O_RDONLY | EvdevNative.O_NONBLOCK);
            if (fd < 0)
            {
                return (false, Marshal.GetLastWin32Error());
            }
            return (true, 0);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return (false, -1);
        }
        finally
        {
            if (fd >= 0)
            {
                _ = EvdevNative.close(fd);
            }
        }
    }

    internal sealed class DeviceOpenException : IOException
    {
        public int Errno { get; }

        public DeviceOpenException()
            : base("Cannot open device.")
        {
            Errno = -1;
        }

        public DeviceOpenException(string message)
            : base(message)
        {
            Errno = -1;
        }

        public DeviceOpenException(string message, Exception innerException)
            : base(message, innerException)
        {
            Errno = -1;
        }

        public DeviceOpenException(string devicePath, int errno)
            : base($"Cannot open {devicePath}. Errno: {errno.ToString(CultureInfo.InvariantCulture)}")
        {
            Errno = errno;
        }
    }
}
