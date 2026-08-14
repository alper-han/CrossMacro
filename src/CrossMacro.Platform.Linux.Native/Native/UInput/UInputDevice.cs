
namespace CrossMacro.Platform.Linux.Native.UInput;

public sealed class UInputDevice(int width = 0, int height = 0) : IUInputDevice
{
    private const int ErrnoNoEntry = 2;
    private const int ErrnoOperationNotPermitted = 1;
    private const int ErrnoPermissionDenied = 13;
    private const string VirtualInputDevicesPath = "/sys/devices/virtual/input";
    private static readonly TimeSpan DeviceReadyTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DeviceReadyPollInterval = TimeSpan.FromMilliseconds(5);

    private int _fd = -1;
    private bool _disposed;
    private readonly int _width = width;
    private readonly int _height = height;
    private readonly UInputAbsolutePacketState _absolutePacketState = new(width, height);

    public bool SupportsAbsoluteCoordinates => UInputDeviceCoordinatePolicy.SupportsAbsoluteCoordinates(_width, _height);

    public void CreateVirtualInputDevice()
    {
        try
        {
            SetupDeviceInternal();
            WaitForDeviceReady();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            CleanupOnFailure();
            throw;
        }
    }

    public async Task CreateVirtualInputDeviceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetupDeviceInternal();
            await WaitForDeviceReadyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            CleanupOnFailure();
            throw;
        }
    }

    private void CleanupOnFailure()
    {
        if (_fd >= 0)
        {
            _ = UInputNative.close(_fd);
            _fd = -1;
        }
    }

    private void WaitForDeviceReady()
    {
        var sysname = TryGetVirtualDeviceSysname();
        if (sysname is null || !Directory.Exists(VirtualInputDevicesPath))
        {
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(startedAt) < DeviceReadyTimeout)
        {
            var node = TryFindEventNode(sysname, out var canInspect);
            if (node is not null)
            {
                Log.Debug("[UInputDevice] Virtual device {Node} is ready", node);
                return;
            }

            if (!canInspect)
            {
                return;
            }

            Thread.Sleep(DeviceReadyPollInterval);
        }

        Log.Debug("[UInputDevice] Virtual device event node was not visible before readiness timeout");
    }

    private async Task WaitForDeviceReadyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sysname = TryGetVirtualDeviceSysname();
        if (sysname is null || !Directory.Exists(VirtualInputDevicesPath))
        {
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(startedAt) < DeviceReadyTimeout)
        {
            var node = TryFindEventNode(sysname, out var canInspect);
            if (node is not null)
            {
                Log.Debug("[UInputDevice] Virtual device {Node} is ready", node);
                return;
            }

            if (!canInspect)
            {
                return;
            }

            var remaining = DeviceReadyTimeout - Stopwatch.GetElapsedTime(startedAt);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(
                    remaining < DeviceReadyPollInterval ? remaining : DeviceReadyPollInterval,
                    TimeProvider.System,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        Log.Debug("[UInputDevice] Virtual device event node was not visible before readiness timeout");
    }

    private string? TryGetVirtualDeviceSysname()
    {
        try
        {
            byte[] buffer = new byte[64];
            if (UInputNative.ioctl(_fd, UInputNative.UI_GET_SYSNAME_64, buffer) < 0)
            {
                return null;
            }

            var sysname = Encoding.ASCII.GetString(buffer).TrimEnd('\0');
            return string.IsNullOrWhiteSpace(sysname) ? null : sysname;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[UInputDevice] Unable to resolve virtual input sysname");
            return null;
        }
    }

    private static string? TryFindEventNode(string sysname, out bool canInspect)
    {
        try
        {
            var devicePath = Path.Combine(VirtualInputDevicesPath, sysname);
            if (!Directory.Exists(devicePath))
            {
                canInspect = true;
                return null;
            }

            var eventPath = Directory.EnumerateDirectories(devicePath, "event*").FirstOrDefault();
            if (eventPath is null)
            {
                canInspect = true;
                return null;
            }

            var eventNode = Path.GetFileName(eventPath);
            canInspect = true;
            return File.Exists(Path.Combine("/dev/input", eventNode)) ? eventNode : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[UInputDevice] Unable to inspect virtual input device state");
            canInspect = false;
            return null;
        }
    }

    private void SetupDeviceInternal()
    {
        Log.Information("[UInputDevice] Creating virtual input device (Mouse + Keyboard, Resolution: {Width}x{Height})...", _width, _height);

        OpenDevice();

        Log.Debug("[UInputDevice] Opened {UInputPath} with fd: {Fd}", LinuxSystemPaths.UInputDevicePath, _fd);
        ConfigureDeviceCapabilities();
        WriteDeviceDefinition();

        int createResult = UInputNative.ioctl(_fd, UInputNative.UI_DEV_CREATE, 0);
        if (createResult < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            Log.LogError("[UInputDevice] Failed to create device (UI_DEV_CREATE). Errno: {Errno}", errno);
            throw new InvalidOperationException($"Failed to create device (Errno: {errno.ToString(CultureInfo.InvariantCulture)})");
        }

        Log.Information("[UInputDevice] Virtual input device (mouse + keyboard) created successfully.");
    }

    private void OpenDevice()
    {
        var primaryErrno = 0;
        var alternateErrno = 0;
        _fd = UInputNative.open(LinuxSystemPaths.UInputDevicePath, UInputNative.O_WRONLY | UInputNative.O_NONBLOCK);
        if (_fd < 0)
        {
            primaryErrno = Marshal.GetLastWin32Error();
            _fd = UInputNative.open(LinuxSystemPaths.UInputAlternatePath, UInputNative.O_WRONLY | UInputNative.O_NONBLOCK);
            if (_fd < 0)
            {
                alternateErrno = Marshal.GetLastWin32Error();
            }
        }

        if (_fd >= 0)
        {
            return;
        }

        var errno = SelectOpenUInputErrno(primaryErrno, alternateErrno);
        Log.LogError(
            "[UInputDevice] Failed to open uinput paths {PrimaryPath} (errno: {PrimaryErrno}) and {AlternatePath} (errno: {AlternateErrno}). Selected errno: {Errno}",
            LinuxSystemPaths.UInputDevicePath, primaryErrno, LinuxSystemPaths.UInputAlternatePath, alternateErrno, errno);
        throw new IOException(BuildOpenUInputErrorMessage(errno));
    }

    private void ConfigureDeviceCapabilities()
    {
        EnableBit(UInputNative.UI_SET_PROPBIT, UInputNative.INPUT_PROP_POINTER);
        EnableBit(UInputNative.UI_SET_EVBIT, UInputNative.EV_KEY);
        EnableBit(UInputNative.UI_SET_KEYBIT, UInputNative.BTN_LEFT);
        EnableBit(UInputNative.UI_SET_KEYBIT, UInputNative.BTN_RIGHT);
        EnableBit(UInputNative.UI_SET_KEYBIT, UInputNative.BTN_MIDDLE);

        if (UInputDeviceCoordinatePolicy.SupportsAbsoluteCoordinates(_width, _height))
        {
            EnableBit(UInputNative.UI_SET_EVBIT, UInputNative.EV_ABS);
            EnableBit(UInputNative.UI_SET_ABSBIT, UInputNative.ABS_X);
            EnableBit(UInputNative.UI_SET_ABSBIT, UInputNative.ABS_Y);
            EnableBit(UInputNative.UI_SET_EVBIT, UInputNative.EV_REL);
            EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_WHEEL);
            EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_HWHEEL);
            EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_X);
            EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_Y);
            Log.Information("[UInputDevice] Creating ABSOLUTE mode device (EV_ABS + EV_REL hybrid)");
        }
        else
        {
            EnableBit(UInputNative.UI_SET_EVBIT, UInputNative.EV_REL);
            EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_X);
            EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_Y);
            EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_WHEEL);
            EnableBit(UInputNative.UI_SET_RELBIT, UInputNative.REL_HWHEEL);
            Log.Information("[UInputDevice] Creating RELATIVE mode device");
        }

        for (int keyCode = 1; keyCode <= VirtualDeviceConstants.MaxKeyCode; keyCode++)
        {
            EnableBit(UInputNative.UI_SET_KEYBIT, keyCode);
        }
    }

    private void WriteDeviceDefinition()
    {
        var uidev = new UInputNative.uinput_user_dev
        {
            name = VirtualDeviceConstants.DeviceName,
            id_bustype = UInputNative.BUS_VIRTUAL,
            id_vendor = VirtualDeviceConstants.VendorId,
            id_product = VirtualDeviceConstants.ProductId,
            id_version = VirtualDeviceConstants.Version,
            absmax = new int[64],
            absmin = new int[64],
            absfuzz = new int[64],
            absflat = new int[64],
        };

        if (UInputDeviceCoordinatePolicy.SupportsAbsoluteCoordinates(_width, _height))
        {
            var (maxX, maxY) = UInputDeviceCoordinatePolicy.GetAbsoluteMaximums(_width, _height);
            uidev.absmax[UInputNative.ABS_X] = maxX;
            uidev.absmax[UInputNative.ABS_Y] = maxY;
        }

        IntPtr size = (IntPtr)Marshal.SizeOf<UInputNative.uinput_user_dev>();
        var uidevPointer = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(uidev, uidevPointer, fDeleteOld: false);
        IntPtr result;
        try
        {
            result = UInputNative.write_setup(_fd, uidevPointer, size);
        }
        finally
        {
            Marshal.DestroyStructure<UInputNative.uinput_user_dev>(uidevPointer);
            Marshal.FreeHGlobal(uidevPointer);
        }

        if (result.ToInt32() < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            Log.LogError("[UInputDevice] Failed to write uinput_user_dev. Errno: {Errno}", errno);
            throw new InvalidOperationException($"Failed to write uinput_user_dev (Errno: {errno.ToString(CultureInfo.InvariantCulture)})");
        }
    }

    private void EnableBit(uint request, int bit)
    {
        if (UInputNative.ioctl(_fd, request, bit) < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            Log.LogError("[UInputDevice] Failed to enable bit {Bit} for request {Request}. Errno: {Errno}", bit, request, errno);
            throw new InvalidOperationException($"Failed to enable bit {bit.ToString(CultureInfo.InvariantCulture)} (Errno: {errno.ToString(CultureInfo.InvariantCulture)})");
        }
    }

    public void SendEvent(ushort type, ushort code, int value)
    {
        if (_fd < 0)
        {
            throw new ObjectDisposedException(nameof(UInputDevice), "Cannot write an event after the uinput device has been disposed.");
        }

        if (type is UInputNative.EV_SYN && code is UInputNative.SYN_REPORT)
        {
            var plan = _absolutePacketState.CompletePacket();
            if (plan?.Reassertion is { } reassertion)
            {
                WriteAbsolutePosition(reassertion);
                WriteAbsolutePosition(plan.Value.Target);
                return;
            }

            WriteEvent(type, code, value);
            return;
        }

        WriteEvent(type, code, value);
        _absolutePacketState.Observe(type, code, value);
    }

    private void WriteEvent(ushort type, ushort code, int value)
    {
        var ev = new UInputNative.input_event
        {
            type = type,
            code = code,
            value = value,
            time_sec = IntPtr.Zero,
            time_usec = IntPtr.Zero,
        };

        IntPtr size = (IntPtr)Marshal.SizeOf<UInputNative.input_event>();
        IntPtr result = UInputNative.write(_fd, ref ev, size);

        var expectedBytes = size.ToInt64();
        var actualBytes = result.ToInt64();
        if (actualBytes == expectedBytes)
        {
            return;
        }

        var errno = Marshal.GetLastWin32Error();
        try
        {
            ThrowIfEventWriteIncomplete(type, code, value, expectedBytes, actualBytes, errno);
        }
        catch (IOException exception)
        {
            Log.LogError(exception, "[UInputDevice] Failed to write event");
            throw;
        }
    }

    internal static void ThrowIfEventWriteIncomplete(
        ushort type,
        ushort code,
        int value,
        long expectedBytes,
        long actualBytes,
        int errno)
    {
        if (actualBytes == expectedBytes)
        {
            return;
        }

        throw new IOException(string.Create(
            CultureInfo.InvariantCulture,
            $"uinput event write failed: Type={type}, Code={code}, Value={value}, ExpectedBytes={expectedBytes}, ActualBytes={actualBytes}, Errno={errno}."));
    }

    private void Emit(ushort type, ushort code, int value)
    {
        SendEvent(type, code, value);
    }

    public void Move(int dx, int dy)
    {
        if (_fd < 0)
        {
            return;
        }

        Emit(UInputNative.EV_REL, UInputNative.REL_X, dx);
        Emit(UInputNative.EV_REL, UInputNative.REL_Y, dy);
        Emit(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
    }

    public void MoveAbsolute(int x, int y)
    {
        if (_fd < 0)
        {
            return;
        }

        var target = UInputDeviceCoordinatePolicy.ClampAbsoluteCoordinates(x, y, _width, _height);
        Emit(UInputNative.EV_ABS, UInputNative.ABS_X, target.X);
        Emit(UInputNative.EV_ABS, UInputNative.ABS_Y, target.Y);
        Emit(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
    }

    private void WriteAbsolutePosition((int X, int Y) position)
    {
        WriteEvent(UInputNative.EV_ABS, UInputNative.ABS_X, position.X);
        WriteEvent(UInputNative.EV_ABS, UInputNative.ABS_Y, position.Y);
        WriteEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
    }

    public void Click(int buttonCode, bool pressed)
    {
        Emit(UInputNative.EV_KEY, (ushort)buttonCode, pressed ? 1 : 0);
        Emit(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
    }

    public void EmitButton(int buttonCode, bool pressed)
    {
        SendEvent(UInputNative.EV_KEY, (ushort)buttonCode, pressed ? 1 : 0);
        SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
    }

    public void EmitClick(int buttonCode)
    {
        EmitButton(buttonCode, pressed: true);
        EmitButton(buttonCode, pressed: false);
    }

    public void EmitKey(int keyCode, bool pressed)
    {
        SendEvent(UInputNative.EV_KEY, (ushort)keyCode, pressed ? 1 : 0);
        SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_fd >= 0)
            {
                Log.Information("[UInputDevice] Destroying virtual device...");
                _ = UInputNative.ioctl(_fd, UInputNative.UI_DEV_DESTROY, 0);
                _ = UInputNative.close(_fd);
                _fd = -1;
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    internal static string BuildOpenUInputErrorMessage(int errno)
    {
        var baseMessage =
            $"Cannot open {LinuxSystemPaths.UInputDevicePath} or {LinuxSystemPaths.UInputAlternatePath} (Errno: {errno.ToString(CultureInfo.InvariantCulture)}).";

        return errno switch
        {
            ErrnoNoEntry =>
                $"{baseMessage} uinput device node is missing. Load the module (sudo modprobe uinput) and retry.",
            ErrnoPermissionDenied =>
                $"{baseMessage} Permission denied. Ensure daemon user can write /dev/uinput (input or uinput group, distro dependent).",
            ErrnoOperationNotPermitted =>
                $"{baseMessage} Operation not permitted. Check service sandbox/capabilities and uinput access policy.",
            _ =>
                $"{baseMessage} Check that uinput exists and daemon has required permissions.",
        };
    }

    internal static int SelectOpenUInputErrno(int primaryErrno, int alternateErrno)
    {
        if (IsPermissionErrno(primaryErrno))
        {
            return primaryErrno;
        }

        if (IsPermissionErrno(alternateErrno))
        {
            return alternateErrno;
        }

        return primaryErrno is not 0 ? primaryErrno : alternateErrno;
    }

    private static bool IsPermissionErrno(int errno)
    {
        return errno is ErrnoPermissionDenied or ErrnoOperationNotPermitted;
    }
}
