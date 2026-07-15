
namespace CrossMacro.Platform.Windows.Services;

internal interface IWindowsHookInstaller
{
    IntPtr InstallMouseHook(IntPtr moduleHandle, User32.HookProc hookProc);
    IntPtr InstallKeyboardHook(IntPtr moduleHandle, User32.HookProc hookProc);
}

public sealed class WindowsInputCapture : IInputCapture
{
    private const uint LowLevelKeyboardHookFlagExtended = 0x01;
    private const uint LowLevelKeyboardHookFlagLowerIntegrityInjected = 0x02;
    private const uint LowLevelKeyboardHookFlagInjected = 0x10;
    private const uint NotifyForThisSession = 0;
    private const int WtsSessionUnlock = 0x8;
    private const int WtsSessionDesktopReady = 0xF;
    private static readonly IntPtr HwndMessage = new(-3);

    public string ProviderName => "Windows Hooks";
    public bool IsSupported => OperatingSystem.IsWindows();

    public event EventHandler<CapturedInputEventArgs>? InputReceived;
    public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

    private bool _captureMouse;
    private bool _captureKeyboard;

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

    private uint _messagePumpThreadId;
    private CancellationTokenRegistration _startCancellationRegistration;
    private readonly IWindowsHookInstaller _hookInstaller;

    public WindowsInputCapture()
        : this(new DefaultWindowsHookInstaller())
    {
    }

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



    public Task StartAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var startupTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var messagePumpThread = new Thread(() =>
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

                    if (_captureMouse)
                    {
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

                RegisterSessionNotificationWindow();

                startupTcs.TrySetResult();

                while (!ct.IsCancellationRequested)
                {
                    if (User32.GetMessage(out var msg, IntPtr.Zero, 0, 0))
                    {
                        if (msg.message == User32.WM_QUIT)
                        {
                            break;
                        }

                        User32.TranslateMessage(ref msg);
                        User32.DispatchMessage(ref msg);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!startupTcs.TrySetException(ex) && !startupTcs.Task.IsCanceled)
                {
                    CaptureError?.Invoke(this, new InputCaptureErrorEventArgs($"Message pump error: {ex.Message}"));
                }
            }
            finally
            {
                UnregisterSessionNotificationWindow();
                UninstallHooks();
                _messagePumpThreadId = 0;
            }
        });

        messagePumpThread.IsBackground = true;
        messagePumpThread.Start();

        _startCancellationRegistration.Dispose();
        _startCancellationRegistration = ct.Register(() =>
        {
            startupTcs.TrySetCanceled(ct);
            StopCapture();
        });

        return startupTcs.Task;
    }

    public void StopCapture()
    {
        _startCancellationRegistration.Dispose();

        if (_messagePumpThreadId != 0)
        {
            User32.PostThreadMessage(_messagePumpThreadId, User32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private IntPtr InstallMouseHook(IntPtr moduleHandle)
        => _hookInstaller.InstallMouseHook(moduleHandle, _mouseProc!);

    private IntPtr InstallKeyboardHook(IntPtr moduleHandle)
        => _hookInstaller.InstallKeyboardHook(moduleHandle, _keyboardProc!);

    private sealed class DefaultWindowsHookInstaller : IWindowsHookInstaller
    {
        public IntPtr InstallMouseHook(IntPtr moduleHandle, User32.HookProc hookProc)
            => User32.SetWindowsHookEx(User32.WH_MOUSE_LL, hookProc, moduleHandle, 0);

        public IntPtr InstallKeyboardHook(IntPtr moduleHandle, User32.HookProc hookProc)
            => User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, hookProc, moduleHandle, 0);
    }

    private void RegisterSessionNotificationWindow()
    {
        var instanceHandle = Kernel32.GetModuleHandle(lpModuleName: null);
        var windowClass = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = _sessionWindowProc!,
            hInstance = instanceHandle,
            lpszClassName = _sessionWindowClassName,
        };

        if (User32.RegisterClassEx(ref windowClass) is 0)
        {
            Log.Warning("[WindowsInputCapture] Failed to register session notification window class");
            return;
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
            Log.Warning("[WindowsInputCapture] Failed to create session notification window");
            User32.UnregisterClass(_sessionWindowClassName, instanceHandle);
            return;
        }

        if (!WtsApi32.WTSRegisterSessionNotification(_sessionWindowHandle, NotifyForThisSession))
        {
            Log.Warning("[WindowsInputCapture] Failed to register session notifications");
            DestroySessionNotificationWindow(instanceHandle);
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
        if (_sessionNotificationRegistered && _sessionWindowHandle != IntPtr.Zero)
        {
            WtsApi32.WTSUnRegisterSessionNotification(_sessionWindowHandle);
            _sessionNotificationRegistered = false;
        }

        if (_sessionWindowHandle != IntPtr.Zero)
        {
            User32.DestroyWindow(_sessionWindowHandle);
            _sessionWindowHandle = IntPtr.Zero;
        }

        User32.UnregisterClass(_sessionWindowClassName, instanceHandle);
    }

    private IntPtr SessionWindowCallback(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (IsSessionRecoveryMessage(msg, wParam))
        {
            CaptureError?.Invoke(this, new InputCaptureErrorEventArgs("Recovery: Windows session unlocked; restarting input capture."));
        }

        return User32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void UninstallHooks()
    {
        if (_mouseHookHandle != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
        }

        if (_keyboardHookHandle != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_keyboardHookHandle);
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
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            uint msg = (uint)wParam;

            ushort evdevCode = 0;
            int value = 0;
            ushort type = InputEventCode.EV_KEY;

            switch (msg)
            {
                case User32.WM_LBUTTONDOWN:
                    evdevCode = (ushort)InputEventCode.BTN_LEFT;
                    value = 1;
                    break;
                case User32.WM_LBUTTONUP:
                    evdevCode = (ushort)InputEventCode.BTN_LEFT;
                    value = 0;
                    break;
                case User32.WM_RBUTTONDOWN:
                    evdevCode = (ushort)InputEventCode.BTN_RIGHT;
                    value = 1;
                    break;
                case User32.WM_RBUTTONUP:
                    evdevCode = (ushort)InputEventCode.BTN_RIGHT;
                    value = 0;
                    break;
                case User32.WM_MBUTTONDOWN:
                    evdevCode = (ushort)InputEventCode.BTN_MIDDLE;
                    value = 1;
                    break;
                case User32.WM_MBUTTONUP:
                    evdevCode = (ushort)InputEventCode.BTN_MIDDLE;
                    value = 0;
                    break;
                case User32.WM_MOUSEWHEEL:
                    type = InputEventCode.EV_REL;
                    evdevCode = InputEventCode.REL_WHEEL;

                    value = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                    break;

                case User32.WM_MOUSEMOVE:
                    int currentX = hookStruct.pt.x;
                    int currentY = hookStruct.pt.y;

                    if (_firstMove)
                    {
                        _lastX = currentX;
                        _lastY = currentY;
                        _firstMove = false;
                    }

                    int deltaX = currentX - _lastX;
                    int deltaY = currentY - _lastY;

                    _lastX = currentX;
                    _lastY = currentY;

                    if (deltaX is not 0)
                    {
                        var xArgs = new CapturedInputEvent
                        {
                            Type = InputEventType.MouseMove,
                            Code = InputEventCode.REL_X,
                            Value = deltaX,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            DeviceName = "VirtualMouse",
                        };
                        InputReceived?.Invoke(this, new CapturedInputEventArgs(xArgs));
                    }

                    if (deltaY is not 0)
                    {
                        var yArgs = new CapturedInputEvent
                        {
                            Type = InputEventType.MouseMove,
                            Code = InputEventCode.REL_Y,
                            Value = deltaY,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            DeviceName = "VirtualMouse",
                        };
                        InputReceived?.Invoke(this, new CapturedInputEventArgs(yArgs));
                    }

                    // Emit SYNC to flush the movement buffer in MacroRecorder
                    if (deltaX is not 0 || deltaY is not 0)
                    {
                        var syncArgs = new CapturedInputEvent
                        {
                            Type = InputEventType.Sync,
                            Code = 0,
                            Value = 0,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            DeviceName = "VirtualMouse",
                        };
                        InputReceived?.Invoke(this, new CapturedInputEventArgs(syncArgs));
                    }

                    evdevCode = 0;
                    break;
            }

            if (evdevCode is not 0)
            {
                InputEventType eventType;
                if (type == InputEventCode.EV_KEY && evdevCode >= 272 && evdevCode <= 279)
                {
                    eventType = InputEventType.MouseButton;
                }
                else if (type == InputEventCode.EV_REL && evdevCode == InputEventCode.REL_WHEEL)
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
                    DeviceName = "VirtualMouse",
                };
                InputReceived?.Invoke(this, new CapturedInputEventArgs(args));
            }
        }
        return User32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            uint msg = (uint)wParam;

            if (ShouldIgnoreKeyboardHookEvent(hookStruct.flags, hookStruct.dwExtraInfo))
            {
                return User32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }

            bool isDown = msg is User32.WM_KEYDOWN or User32.WM_SYSKEYDOWN;
            bool isUp = msg is User32.WM_KEYUP or User32.WM_SYSKEYUP;

            if (isDown || isUp)
            {
                int evdevCode = WindowsKeyMap.GetEvdevCode((ushort)hookStruct.vkCode);

                // Debug logging for key analysis
                if (isDown)
                {
                    Log.Debug("[WindowsInputCapture] KeyDown: VK={VK} (0x{VKHex}), Scan={Scan}, Flags={Flags}, Mapped={Evdev}",
                        hookStruct.vkCode, hookStruct.vkCode.ToString("X", CultureInfo.InvariantCulture), hookStruct.scanCode, hookStruct.flags, evdevCode);
                }

                // Handle Extended Keys (distinguish Numpad vs Standard)
                bool isExtended = (hookStruct.flags & LowLevelKeyboardHookFlagExtended) == LowLevelKeyboardHookFlagExtended;

                // Fix for Right Alt (AltGr) appearing as Generic Menu (0x12) or Left Alt (0xA4) with Extended flag
                if ((hookStruct.vkCode == 0x12 || hookStruct.vkCode == 0xA4) && isExtended)
                {
                    evdevCode = InputEventCode.KEY_RIGHTALT;
                }
                // Fix for Right Ctrl appearing as Generic Control (0x11) or Left Ctrl (0xA2) with Extended flag
                if ((hookStruct.vkCode == 0x11 || hookStruct.vkCode == 0xA2) && isExtended)
                {
                    evdevCode = InputEventCode.KEY_RIGHTCTRL;
                }

                // Numpad Enter (Extended) vs Return
                if (hookStruct.vkCode == 0x0D && isExtended)
                {
                    evdevCode = InputEventCode.KEY_KPENTER;
                }

                if (evdevCode is not 0)
                {
                    var args = new CapturedInputEvent
                    {
                        Type = (InputEventType)InputEventCode.EV_KEY,
                        Code = (ushort)evdevCode,
                        Value = isDown ? 1 : 0,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        DeviceName = "VirtualKeyboard",
                    };
                    InputReceived?.Invoke(this, new CapturedInputEventArgs(args));
                }
                else if (isDown)
                {
                    Log.Warning("[WindowsInputCapture] Unmapped key: VK={VK} (0x{VKHex})", hookStruct.vkCode, hookStruct.vkCode.ToString("X", CultureInfo.InvariantCulture));
                }
            }
        }
        return User32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    internal static bool ShouldIgnoreKeyboardHookEvent(uint hookFlags, IntPtr extraInfo)
    {
        var isInjected = (hookFlags & (LowLevelKeyboardHookFlagInjected | LowLevelKeyboardHookFlagLowerIntegrityInjected)) != 0;
        return isInjected && extraInfo == InputEventMarkers.ToIntPtr(InputEventMarkers.TextExpansionKeyboardEvent);
    }

    internal static bool IsSessionRecoveryMessage(uint message, IntPtr wParam)
    {
        if (message != User32.WM_WTSSESSION_CHANGE)
        {
            return false;
        }

        var reason = wParam.ToInt32();
        return reason is WtsSessionUnlock or WtsSessionDesktopReady;
    }
}
