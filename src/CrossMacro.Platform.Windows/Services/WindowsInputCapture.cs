
namespace CrossMacro.Platform.Windows.Services;

public sealed class WindowsInputCapture : IInputCapture, IMouseCoordinateModeInputCapture
{
    private const uint NotifyForThisSession = 0;
    private static readonly IntPtr HwndMessage = new(-3);

    public string ProviderName => "Windows Hooks";
    public bool IsSupported => OperatingSystem.IsWindows();

    public event EventHandler<CapturedInputEventArgs>? InputReceived;
    public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

    private bool _captureMouse;
    private bool _captureKeyboard;
    private bool _useAbsoluteCoordinates;
    private bool _useRawRelativeCoordinates;

    private int _lastX;
    private int _lastY;
    private bool _firstMove = true;

    private IntPtr _mouseHookHandle = IntPtr.Zero;
    private IntPtr _keyboardHookHandle = IntPtr.Zero;
    private IntPtr _sessionWindowHandle = IntPtr.Zero;
    private User32.HookProc? _mouseProc;
    private User32.HookProc? _keyboardProc;
    private User32.WindowProc? _sessionWindowProc;
    private readonly string _sessionWindowClassName = $"CrossMacroSessionSwitch_{Guid.NewGuid():N}";
    private bool _sessionNotificationRegistered;
    private bool _rawMouseInputRegistered;
    private IntPtr _rawInputBuffer;
    private int _rawInputBufferSize;

    private uint _messagePumpThreadId;
    private CancellationTokenRegistration _startCancellationRegistration;
    private readonly IWindowsHookInstaller _hookInstaller;

    public WindowsInputCapture()
        : this(new DefaultWindowsHookInstaller()) { /* Empty */ }

    internal WindowsInputCapture(IWindowsHookInstaller hookInstaller)
    {
        ArgumentNullException.ThrowIfNull(hookInstaller);
        _hookInstaller = hookInstaller;
    }

    public void Configure(bool captureMouse, bool captureKeyboard)
    {
        _captureMouse = captureMouse;
        _captureKeyboard = captureKeyboard;
    }

    public void ConfigureCoordinateMode(
        bool useAbsoluteCoordinates,
        bool useLogicalCoordinates)
    {
        _useAbsoluteCoordinates = useAbsoluteCoordinates;
        _useRawRelativeCoordinates = !useAbsoluteCoordinates && !useLogicalCoordinates;
    }


    public async Task StartAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var startupTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var messagePumpThread = new Thread(() => RunMessagePumpThread(startupTcs, ct))
        {
            IsBackground = true,
        };

        messagePumpThread.Start();

        await _startCancellationRegistration.DisposeAsync().ConfigureAwait(false);
        _startCancellationRegistration = ct.Register(() =>
        {
            _ = startupTcs.TrySetCanceled(ct);
            StopCapture();
        });

        await startupTcs.Task.ConfigureAwait(false);
    }

    private void RunMessagePumpThread(TaskCompletionSource startupTcs, CancellationToken ct)
    {
        try
        {
            _messagePumpThreadId = Kernel32.GetCurrentThreadId();

            _mouseProc = MouseHookCallback;
            _keyboardProc = KeyboardHookCallback;
            _sessionWindowProc = SessionWindowCallback;

            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                IntPtr moduleHandle = Kernel32.GetModuleHandle(curModule?.ModuleName);
                InstallConfiguredHooks(moduleHandle);
            }

            RegisterSessionNotificationWindow();
            _ = startupTcs.TrySetResult();
            RunWindowsMessageLoop(ct);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (ex is OutOfMemoryException)
            {
                throw;
            }

            if (!startupTcs.TrySetException(ex) && !startupTcs.Task.IsCanceled)
            {
                CaptureError?.Invoke(this, new InputCaptureErrorEventArgs($"Message pump error: {ex.Message}"));
            }
        }
        finally
        {
            UnregisterSessionNotificationWindow();
            UninstallHooks();
            ReleaseRawInputBuffer();
            _messagePumpThreadId = 0;
        }
    }

    private void InstallConfiguredHooks(IntPtr moduleHandle)
    {
        if (_captureMouse)
        {
            InitializeMousePosition();
            _mouseHookHandle = InstallMouseHook(moduleHandle);
            if (_mouseHookHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to install mouse hook");
            }
        }

        if (_captureKeyboard)
        {
            _keyboardHookHandle = InstallKeyboardHook(moduleHandle);
            if (_keyboardHookHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to install keyboard hook");
            }
        }
    }

    private void InitializeMousePosition()
    {
        _firstMove = !User32.GetCursorPos(out var position);
        if (!_firstMove)
        {
            _lastX = position.x;
            _lastY = position.y;
        }
    }

    private static void RunWindowsMessageLoop(CancellationToken ct)
        => WindowsMessagePump.Run(ct);

    public void StopCapture()
    {
        _startCancellationRegistration.Dispose();

        if (_messagePumpThreadId != 0)
        {
            WindowsMessagePump.RequestStop(_messagePumpThreadId);
        }
    }

    private IntPtr InstallMouseHook(IntPtr moduleHandle)
        => _hookInstaller.InstallMouseHook(moduleHandle, _mouseProc!);

    private IntPtr InstallKeyboardHook(IntPtr moduleHandle)
        => _hookInstaller.InstallKeyboardHook(moduleHandle, _keyboardProc!);

    private void RegisterSessionNotificationWindow()
    {
        var instanceHandle = Kernel32.GetModuleHandle(lpModuleName: null);
        var classNamePointer = Marshal.StringToCoTaskMemUni(_sessionWindowClassName);
        var windowProcPointer = Marshal.GetFunctionPointerForDelegate(_sessionWindowProc!);
        var windowClass = new WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            lpfnWndProc = windowProcPointer,
            hInstance = instanceHandle,
            lpszClassName = classNamePointer,
        };

        try
        {
            if (User32.RegisterClassEx(ref windowClass) is 0)
            {
                ThrowIfRawInputRequired("register the raw input window class");
                Log.Warning("[WindowsInputCapture] Failed to register session notification window class");
                return;
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(classNamePointer);
        }

        _sessionWindowHandle = User32.CreateWindowEx(
            0,
            _sessionWindowClassName,
            _sessionWindowClassName,
            0,
            0,
            0,
            0,
            0,
            HwndMessage,
            IntPtr.Zero,
            instanceHandle,
            IntPtr.Zero);

        if (_sessionWindowHandle == IntPtr.Zero)
        {
            ThrowIfRawInputRequired("create the raw input window");
            Log.Warning("[WindowsInputCapture] Failed to create session notification window");
            _ = User32.UnregisterClass(_sessionWindowClassName, instanceHandle);
            return;
        }

        RegisterRawMouseInput();

        if (!WtsApi32.WTSRegisterSessionNotification(_sessionWindowHandle, NotifyForThisSession))
        {
            Log.Warning("[WindowsInputCapture] Failed to register session notifications");
            return;
        }

        _sessionNotificationRegistered = true;
    }

    private void UnregisterSessionNotificationWindow()
    {
        DestroySessionNotificationWindow(Kernel32.GetModuleHandle(lpModuleName: null));
    }

    private void DestroySessionNotificationWindow(IntPtr instanceHandle)
    {
        // Raw input registration is process-global. Do not remove it here: an older
        // capture can otherwise unregister a newer capture during a rapid restart.
        if (_sessionNotificationRegistered && _sessionWindowHandle != IntPtr.Zero)
        {
            _ = WtsApi32.WTSUnRegisterSessionNotification(_sessionWindowHandle);
            _sessionNotificationRegistered = false;
        }

        if (_sessionWindowHandle != IntPtr.Zero)
        {
            _ = User32.DestroyWindow(_sessionWindowHandle);
            _sessionWindowHandle = IntPtr.Zero;
        }

        _ = User32.UnregisterClass(_sessionWindowClassName, instanceHandle);
    }

    private IntPtr SessionWindowCallback(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == User32.WM_INPUT && _rawMouseInputRegistered)
        {
            ProcessRawMouseInput(lParam);
        }
        else if (IsSessionRecoveryMessage(msg, wParam))
        {
            CaptureError?.Invoke(this, new InputCaptureErrorEventArgs("Recovery: Windows session unlocked; restarting input capture."));
        }

        return User32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void UninstallHooks()
    {
        if (_mouseHookHandle != IntPtr.Zero)
        {
            _ = User32.UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
        }

        if (_keyboardHookHandle != IntPtr.Zero)
        {
            _ = User32.UnhookWindowsHookEx(_keyboardHookHandle);
            _keyboardHookHandle = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        StopCapture();
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<MsllHookStruct>(lParam);
            uint msg = (uint)wParam;

            if (msg == User32.WM_MOUSEMOVE && !_useRawRelativeCoordinates)
            {
                HandleMouseMove(hookStruct.pt.x, hookStruct.pt.y);
            }
            else if (TryMapMouseButtonOrScroll(msg, hookStruct.mouseData, out ushort evdevCode, out int value, out ushort type))
            {
                EmitMouseButtonOrScrollEvent(evdevCode, value, type);
            }
        }
        return User32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    internal static bool TryMapMouseButtonOrScroll(uint msg, uint mouseData, out ushort evdevCode, out int value, out ushort type)
        => WindowsInputEventPolicy.TryMapMouseButtonOrScroll(msg, mouseData, out evdevCode, out value, out type);

    private void HandleMouseMove(int currentX, int currentY)
    {
        bool hadPreviousPosition = !_firstMove;
        int previousX = _lastX;
        int previousY = _lastY;
        if (_firstMove)
        {
            _lastX = currentX;
            _lastY = currentY;
            _firstMove = false;

            if (!_useAbsoluteCoordinates)
            {
                return;
            }
        }

        if (hadPreviousPosition && currentX == previousX && currentY == previousY)
        {
            return;
        }

        var movement = ResolveMouseMovement(
            _useAbsoluteCoordinates,
            currentX,
            currentY,
            previousX,
            previousY);

        _lastX = currentX;
        _lastY = currentY;
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long timestampMicroseconds = GetMonotonicTimestampMicroseconds();

        if (_useAbsoluteCoordinates || movement.XValue is not 0)
        {
            var xArgs = new CapturedInputEvent
            {
                Type = InputEventType.MouseMove,
                Code = movement.XCode,
                Value = movement.XValue,
                Timestamp = timestamp,
                TimestampMicroseconds = timestampMicroseconds,
                DeviceName = "VirtualMouse",
            };
            InputReceived?.Invoke(this, new CapturedInputEventArgs(xArgs));
        }

        if (_useAbsoluteCoordinates || movement.YValue is not 0)
        {
            var yArgs = new CapturedInputEvent
            {
                Type = InputEventType.MouseMove,
                Code = movement.YCode,
                Value = movement.YValue,
                Timestamp = timestamp,
                TimestampMicroseconds = timestampMicroseconds,
                DeviceName = "VirtualMouse",
            };
            InputReceived?.Invoke(this, new CapturedInputEventArgs(yArgs));
        }

        var syncArgs = new CapturedInputEvent
        {
            Type = InputEventType.Sync,
            Code = 0,
            Value = 0,
            Timestamp = timestamp,
            TimestampMicroseconds = timestampMicroseconds,
            DeviceName = "VirtualMouse",
        };
        InputReceived?.Invoke(this, new CapturedInputEventArgs(syncArgs));
    }

    internal static (ushort XCode, int XValue, ushort YCode, int YValue) ResolveMouseMovement(
        bool useAbsoluteCoordinates,
        int currentX,
        int currentY,
        int previousX,
        int previousY) => WindowsInputEventPolicy.ResolveMouseMovement(
            useAbsoluteCoordinates,
            currentX,
            currentY,
            previousX,
            previousY);

    internal static bool TryResolveRawRelativeMovement(
        ushort flags,
        int deltaX,
        int deltaY,
        out int rawDeltaX,
        out int rawDeltaY)
    {
        rawDeltaX = 0;
        rawDeltaY = 0;
        if ((flags & User32.MouseMoveAbsolute) is not 0 || (deltaX is 0 && deltaY is 0))
        {
            return false;
        }

        rawDeltaX = deltaX;
        rawDeltaY = deltaY;
        return true;
    }

    private void RegisterRawMouseInput()
    {
        _rawMouseInputRegistered = false;
        if (!_captureMouse || !_useRawRelativeCoordinates)
        {
            return;
        }

        var rawMouseDevice = new RawInputDevice
        {
            UsagePage = User32.HidUsagePageGeneric,
            Usage = User32.HidUsageGenericMouse,
            Flags = User32.RidevInputSink,
            TargetWindow = _sessionWindowHandle,
        };
        if (!User32.RegisterRawInputDevices(
                in rawMouseDevice,
                numberOfDevices: 1,
                sizeOfRawInputDevice: (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to register raw mouse input");
        }

        _rawMouseInputRegistered = true;
    }

    private void ThrowIfRawInputRequired(string operation)
    {
        if (_captureMouse && _useRawRelativeCoordinates)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                $"Failed to {operation} for raw mouse input");
        }
    }

    private void ProcessRawMouseInput(IntPtr rawInputHandle)
    {
        uint size = 0;
        uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        if (User32.GetRawInputData(rawInputHandle, User32.RidInput, IntPtr.Zero, ref size, headerSize) == uint.MaxValue
            || size < headerSize + (uint)Marshal.SizeOf<RawMouse>())
        {
            return;
        }

        EnsureRawInputBufferCapacity(checked((int)size));
        uint bytesRead = User32.GetRawInputData(rawInputHandle, User32.RidInput, _rawInputBuffer, ref size, headerSize);
        if (bytesRead == uint.MaxValue || bytesRead < headerSize + (uint)Marshal.SizeOf<RawMouse>())
        {
            return;
        }

        var header = Marshal.PtrToStructure<RawInputHeader>(_rawInputBuffer);
        if (header.Type is not User32.RimTypeMouse)
        {
            return;
        }

        var mouse = Marshal.PtrToStructure<RawMouse>(IntPtr.Add(_rawInputBuffer, (int)headerSize));
        if (TryResolveRawRelativeMovement(mouse.Flags, mouse.LastX, mouse.LastY, out int deltaX, out int deltaY))
        {
            EmitRawMouseMovement(deltaX, deltaY);
        }
    }

    private void EnsureRawInputBufferCapacity(int requiredSize)
    {
        if (_rawInputBufferSize >= requiredSize)
        {
            return;
        }

        _rawInputBuffer = Marshal.ReAllocHGlobal(_rawInputBuffer, (IntPtr)requiredSize);
        _rawInputBufferSize = requiredSize;
    }

    private void ReleaseRawInputBuffer()
    {
        if (_rawInputBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_rawInputBuffer);
            _rawInputBuffer = IntPtr.Zero;
            _rawInputBufferSize = 0;
        }

        _rawMouseInputRegistered = false;
    }

    private void EmitRawMouseMovement(int deltaX, int deltaY)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long timestampMicroseconds = GetMonotonicTimestampMicroseconds();
        if (deltaX is not 0)
        {
            InputReceived?.Invoke(this, new CapturedInputEventArgs(new CapturedInputEvent
            {
                Type = InputEventType.MouseMove,
                Code = InputEventCode.REL_X,
                Value = deltaX,
                Timestamp = timestamp,
                TimestampMicroseconds = timestampMicroseconds,
                DeviceName = "RawMouse",
            }));
        }

        if (deltaY is not 0)
        {
            InputReceived?.Invoke(this, new CapturedInputEventArgs(new CapturedInputEvent
            {
                Type = InputEventType.MouseMove,
                Code = InputEventCode.REL_Y,
                Value = deltaY,
                Timestamp = timestamp,
                TimestampMicroseconds = timestampMicroseconds,
                DeviceName = "RawMouse",
            }));
        }

        InputReceived?.Invoke(this, new CapturedInputEventArgs(new CapturedInputEvent
        {
            Type = InputEventType.Sync,
            Code = 0,
            Value = 0,
            Timestamp = timestamp,
            TimestampMicroseconds = timestampMicroseconds,
            DeviceName = "RawMouse",
        }));
    }

    private void EmitMouseButtonOrScrollEvent(ushort evdevCode, int value, ushort type)
    {
        InputEventType eventType;
        if (type == InputEventCode.EV_KEY && evdevCode >= 272 && evdevCode <= 279)
        {
            eventType = InputEventType.MouseButton;
        }
        else if (type == InputEventCode.EV_REL && evdevCode is InputEventCode.REL_WHEEL or InputEventCode.REL_HWHEEL)
        {
            eventType = InputEventType.MouseScroll;
        }
        else
        {
            eventType = (InputEventType)type;
        }

        var args = new CapturedInputEvent
        {
            Type = eventType,
            Code = evdevCode,
            Value = value,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            TimestampMicroseconds = GetMonotonicTimestampMicroseconds(),
            DeviceName = "VirtualMouse",
        };
        InputReceived?.Invoke(this, new CapturedInputEventArgs(args));
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<KbdllHookStruct>(lParam);
            uint msg = (uint)wParam;

            if (!ShouldIgnoreKeyboardHookEvent(hookStruct.flags, hookStruct.dwExtraInfo))
            {
                HandleKeyboardEvent(msg, hookStruct);
            }
        }
        return User32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void HandleKeyboardEvent(uint msg, KbdllHookStruct hookStruct)
    {
        bool isDown = msg is User32.WM_KEYDOWN or User32.WM_SYSKEYDOWN;
        bool isUp = msg is User32.WM_KEYUP or User32.WM_SYSKEYUP;

        if (!isDown && !isUp)
        {
            return;
        }

        int evdevCode = MapKeyboardEvent((ushort)hookStruct.vkCode, hookStruct.flags);

        // Debug logging for key analysis
        if (isDown)
        {
            Log.Debug("[WindowsInputCapture] KeyDown: VK={VK} (0x{VKHex}), Scan={Scan}, Flags={Flags}, Mapped={Evdev}",
                hookStruct.vkCode, hookStruct.vkCode.ToString("X", CultureInfo.InvariantCulture), hookStruct.scanCode, hookStruct.flags, evdevCode);
        }

        if (evdevCode is not 0)
        {
            var args = new CapturedInputEvent
            {
                Type = (InputEventType)InputEventCode.EV_KEY,
                Code = (ushort)evdevCode,
                Value = isDown ? 1 : 0,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                TimestampMicroseconds = GetMonotonicTimestampMicroseconds(),
                DeviceName = "VirtualKeyboard",
            };
            InputReceived?.Invoke(this, new CapturedInputEventArgs(args));
        }
        else if (isDown)
        {
            Log.Warning("[WindowsInputCapture] Unmapped key: VK={VK} (0x{VKHex})", hookStruct.vkCode, hookStruct.vkCode.ToString("X", CultureInfo.InvariantCulture));
        }
    }

    internal static int MapKeyboardEvent(ushort virtualKey, uint hookFlags)
        => WindowsInputEventPolicy.MapKeyboardEvent(virtualKey, hookFlags);

    internal static bool ShouldIgnoreKeyboardHookEvent(uint hookFlags, IntPtr extraInfo)
        => WindowsInputEventPolicy.ShouldIgnoreKeyboardHookEvent(hookFlags, extraInfo);

    internal static bool IsSessionRecoveryMessage(uint message, IntPtr wParam)
        => WindowsInputEventPolicy.IsSessionRecoveryMessage(message, wParam);

    internal static long GetMonotonicTimestampMicroseconds() =>
        ToMicroseconds(Stopwatch.GetTimestamp(), Stopwatch.Frequency);

    internal static long ToMicroseconds(long timestamp, long frequency)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frequency, 0);
        return checked(
            (timestamp / frequency * 1_000_000L)
            + (timestamp % frequency * 1_000_000L / frequency));
    }
}
