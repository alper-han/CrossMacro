
namespace CrossMacro.Platform.Linux.Native.UInput;

public sealed partial class UInputDevice(int width = 0, int height = 0) : IUInputDevice
{
    private const int ErrnoNoEntry = 2;
    private const int ErrnoOperationNotPermitted = 1;
    private const int ErrnoPermissionDenied = 13;

    private int _fd = -1;
    private bool _disposed;
    private readonly int _width = width;
    private readonly int _height = height;

    public bool SupportsAbsoluteCoordinates => _width > 0 && _height > 0;

    public void CreateVirtualInputDevice()
    {
        try
        {
            SetupDeviceInternal();
            WaitForDeviceStabilization();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            CleanupOnFailure();
            throw;
        }
    }

    public async Task CreateVirtualInputDeviceAsync()
    {
        try
        {
            SetupDeviceInternal();
            await WaitForDeviceStabilizationAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            CleanupOnFailure();
            throw;
        }
    }

    [LibraryImport("libc", SetLastError = true, EntryPoint = "ioctl")]
    private static partial int ioctl_sysname(int fd, uint request, byte[] name);

    private const uint UI_GET_SYSNAME_64 = 0x8040552c;

    private string? FindVirtualDeviceEventNode()
    {
        try
        {
            byte[] buf = new byte[64];
            int result = ioctl_sysname(_fd, UI_GET_SYSNAME_64, buf);
            if (result < 0)
            {
                return null;
            }

            string sysname = System.Text.Encoding.ASCII.GetString(buf).TrimEnd('\0');
            if (string.IsNullOrWhiteSpace(sysname))
            {
                return null;
            }

            var sysPath = $"/sys/devices/virtual/input/{sysname}";
            if (!System.IO.Directory.Exists(sysPath))
            {
                return null;
            }

            var dirs = System.IO.Directory.GetDirectories(sysPath, "event*");
            if (dirs.Length > 0)
            {
                return System.IO.Path.GetFileName(dirs[0]);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Ignore and fallback
        }
        return null;
    }

    private void WaitForDeviceStabilization()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 500)
        {
            var eventNode = FindVirtualDeviceEventNode();
            if (eventNode is not null)
            {
                var path = $"/dev/input/{eventNode}";
                if (System.IO.File.Exists(path))
                {
                    Log.Debug("[UInputDevice] Virtual device {Node} detected and stabilized in {Elapsed}ms", eventNode, stopwatch.ElapsedMilliseconds);
                    return;
                }
            }
            Thread.Sleep(5);
        }
        Log.Warning("[UInputDevice] Virtual device stabilization timed out after 500ms.");
    }

    private async Task WaitForDeviceStabilizationAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 500)
        {
            var eventNode = FindVirtualDeviceEventNode();
            if (eventNode is not null)
            {
                var path = $"/dev/input/{eventNode}";
                if (System.IO.File.Exists(path))
                {
                    Log.Debug("[UInputDevice] Virtual device {Node} detected and stabilized in {Elapsed}ms (async)", eventNode, stopwatch.ElapsedMilliseconds);
                    return;
                }
            }
            await Task.Delay(5, CancellationToken.None).ConfigureAwait(false);
        }
        Log.Warning("[UInputDevice] Virtual device stabilization timed out after 500ms (async).");
    }

    private void CleanupOnFailure()
    {
        if (_fd >= 0)
        {
            _ = UInputNative.close(_fd);
            _fd = -1;
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

        if (_width > 0 && _height > 0)
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

        if (_width > 0 && _height > 0)
        {
            uidev.absmax[UInputNative.ABS_X] = _width - 1;
            uidev.absmax[UInputNative.ABS_Y] = _height - 1;
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
            return;
        }

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

        if (result.ToInt32() < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            Log.Warning("[UInputDevice] Failed to write event. Errno: {Errno}", errno);
        }
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

        if (_width > 0)
        {
            x = Math.Clamp(x, 0, _width - 1);
        }

        if (_height > 0)
        {
            y = Math.Clamp(y, 0, _height - 1);
        }

        Emit(UInputNative.EV_ABS, UInputNative.ABS_X, x);
        Emit(UInputNative.EV_ABS, UInputNative.ABS_Y, y);
        Emit(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
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
