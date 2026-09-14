
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

    private static readonly InputDeviceDiscovery Discovery = new(new NativeInputDeviceDiscoverySource());

    public static IReadOnlyList<InputDevice> GetAvailableDevices()
        => GetAvailableDevices(logSummary: true);

    public static IReadOnlyList<InputDevice> GetAvailableDevices(bool logSummary, bool logInaccessibleWarning = true)
        => Discovery.GetAvailableDevices(logSummary, logInaccessibleWarning);

    public static Task<IReadOnlyList<InputDevice>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
        => GetAvailableDevicesAsync(logInaccessibleWarning: true, cancellationToken);

    public static Task<IReadOnlyList<InputDevice>> GetAvailableDevicesAsync(bool logInaccessibleWarning, CancellationToken cancellationToken = default)
        => Discovery.GetAvailableDevicesAsync(logInaccessibleWarning, cancellationToken);

    internal static InputDevice GetDeviceInfo(string devicePath, ProcInputDeviceSnapshot procSnapshot)
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

            var capabilities = new EvdevDeviceCapabilities((eventType, bitmap) =>
                EvdevNative.ioctl(fd, EvdevNative.EVIOCGBIT(eventType, bitmap.Length), bitmap));

            bool isMouse = procSnapshot.HasHandler(devicePath, name, InputDeviceHandlers.Mouse) ||
                           capabilities.IsMouse() ||
                           capabilities.IsTouchpad();

            bool isKeyboard = capabilities.IsKeyboard() ||
                              procSnapshot.HasHandler(devicePath, name, InputDeviceHandlers.Keyboard);

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

            for (int keyCode = 0; keyCode <= EvdevDeviceCapabilities.MaximumKeyCode; keyCode++)
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

    internal static InputDevice CreateInaccessiblePlaceholder(string devicePath)
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

    internal static (bool canOpen, int errno) CanOpenForReading(string devicePath)
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
