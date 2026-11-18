using System.ComponentModel;
using System.Diagnostics;

namespace CrossMacro.Platform.Windows.Services.Input;

internal sealed class WindowsCaptureSession : IWindowsCaptureSession
{
    private const uint NotifyForThisSession = 0;

    public string ProviderName => "Windows Hooks";
    public bool IsSupported => OperatingSystem.IsWindows();

    public event EventHandler<CapturedInputEventArgs>? InputReceived;
    public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

    private bool _captureMouse;
    private bool _captureKeyboard;
    private bool _useAbsoluteCoordinates;
    private bool _useRawRelativeCoordinates;

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
    private InputEventDispatcher? _dispatcher;
    private WindowsHookEventTranslator? _translator;
    private string? _terminalError;

    private uint _messagePumpThreadId;
    private CancellationTokenRegistration _startCancellationRegistration;
    private readonly IWindowsHookInstaller _hookInstaller;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _started;
    private volatile bool _stopRequested;

    public Task Completion => _completion.Task;
    public bool IsStopRequested => _stopRequested;

    public WindowsCaptureSession()
        : this(new DefaultWindowsHookInstaller()) { /* Empty */ }

    internal WindowsCaptureSession(IWindowsHookInstaller hookInstaller)
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
        if (ct.IsCancellationRequested)
        {
            _ = _completion.TrySetResult();
        }
        ct.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref _started, 1) is not 0)
        {
            throw new InvalidOperationException("A native capture session can only be started once.");
        }

        var startupTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var messagePumpThread = new Thread(() => RunMessagePumpThread(startupTcs, ct))
        {
            IsBackground = true,
            Name = "WindowsInputCapture",
        };

        _startCancellationRegistration = ct.Register(() =>
        {
            _ = startupTcs.TrySetCanceled(ct);
            StopCapture();
        });
        try { messagePumpThread.Start(); }
        catch
        {
            await _startCancellationRegistration.DisposeAsync().ConfigureAwait(false);
            _ = _completion.TrySetResult();
            throw;
        }

        await startupTcs.Task.ConfigureAwait(false);
    }

    private void RunMessagePumpThread(TaskCompletionSource startupTcs, CancellationToken ct)
    {
        try
        {
            _messagePumpThreadId = Kernel32.GetCurrentThreadId();
            ct.ThrowIfCancellationRequested();
            if (_stopRequested)
            {
                _ = startupTcs.TrySetCanceled(ct);
                return;
            }

            _dispatcher = new InputEventDispatcher("WindowsInputDispatch", DispatchInput, ReportDispatchError);
            _translator = new WindowsHookEventTranslator(Enqueue, _useAbsoluteCoordinates, _useRawRelativeCoordinates);
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
            if (_stopRequested)
            {
                _ = startupTcs.TrySetCanceled(ct);
                return;
            }
            _ = startupTcs.TrySetResult();
            RunWindowsMessageLoop(ct);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (!startupTcs.TrySetException(ex) && !startupTcs.Task.IsCanceled)
            {
                _ = Interlocked.CompareExchange(ref _terminalError, $"Message pump error: {ex.Message}", comparand: null);
            }
        }
        finally
        {
            _stopRequested = true;
            try
            {
                try { UnregisterSessionNotificationWindow(); }
                finally
                {
                    try { UninstallHooks(); }
                    finally
                    {
                        ReleaseRawInputBuffer();
                        _messagePumpThreadId = 0;
                        _startCancellationRegistration.Dispose();
                        ShutDownDispatcher();
                    }
                }
            }
            finally
            {
                _ = _completion.TrySetResult();
            }
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
        if (User32.GetCursorPos(out var position))
        {
            _translator!.SetCursorPosition(position.x, position.y);
        }
    }

    private static void RunWindowsMessageLoop(CancellationToken ct)
        => WindowsMessagePump.Run(ct);

    public void StopCapture()
    {
        _stopRequested = true;
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
            User32.HwndMessage,
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
        else if (WindowsInputEventPolicy.IsSessionRecoveryMessage(msg, wParam))
        {
            QueueCaptureError("Recovery: Windows session unlocked; restarting input capture.");
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
            _translator!.OnMouseHook((uint)wParam, hookStruct);
        }

        return User32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<KbdllHookStruct>(lParam);
            _translator!.OnKeyboardHook((uint)wParam, hookStruct);
        }

        return User32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
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
        if (WindowsInputEventPolicy.TryResolveRawRelativeMovement(mouse.Flags, mouse.LastX, mouse.LastY, out int deltaX, out int deltaY))
        {
            _translator!.OnRawMouseMovement(deltaX, deltaY);
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

    private void Enqueue(CapturedInputEvent inputEvent)
    {
        if (_dispatcher!.TryEnqueue(inputEvent) || _stopRequested)
        {
            return;
        }

        _ = Interlocked.CompareExchange(
            ref _terminalError,
            $"The Windows input event queue exceeded {InputEventDispatcher.DefaultCapacity} events; capture was stopped to avoid recording incomplete input.",
            comparand: null);
        StopCapture();
    }

    private void DispatchInput(CapturedInputEvent inputEvent) =>
        InputReceived?.Invoke(this, new CapturedInputEventArgs(inputEvent));

    private void ReportDispatchError(Exception exception) =>
        RaiseCaptureError($"Error processing captured input: {exception.Message}");

    private void QueueCaptureError(string message) =>
        ThreadPool.UnsafeQueueUserWorkItem(
            static state => state.Session.RaiseCaptureError(state.Message),
            (Session: this, Message: message),
            preferLocal: false);

    private void RaiseCaptureError(string message)
    {
        try
        {
            CaptureError?.Invoke(this, new InputCaptureErrorEventArgs(message));
        }
        catch (Exception errorHandlerException) when (errorHandlerException is not OutOfMemoryException)
        {
            Log.LogError(errorHandlerException, "[WindowsInputCapture] Capture error handler threw");
        }
    }

    private void ShutDownDispatcher()
    {
        if (_dispatcher is { } dispatcher)
        {
            dispatcher.Dispose();
            if (!dispatcher.IsCompleted)
            {
                Log.Warning("[WindowsInputCapture] The input dispatch thread is still running a subscriber after the shutdown timeout");
            }
        }

        if (Interlocked.Exchange(ref _terminalError, value: null) is { } terminalError)
        {
            RaiseCaptureError(terminalError);
        }
    }
}
