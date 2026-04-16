using System.Diagnostics;
using System.Runtime.InteropServices;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Windows.Helpers;
using CrossMacro.Platform.Windows.Native;
using Serilog;

namespace CrossMacro.Platform.Windows.Services;

public class WindowsInputCapture : IInputCapture
{
    private const uint LowLevelKeyboardHookFlagExtended = 0x01;
    private const uint LowLevelKeyboardHookFlagLowerIntegrityInjected = 0x02;
    private const uint LowLevelKeyboardHookFlagInjected = 0x10;

    public string ProviderName => "Windows Hooks";
    public bool IsSupported => OperatingSystem.IsWindows();
    
    public event EventHandler<InputCaptureEventArgs>? InputReceived;
    public event EventHandler<string>? Error;

    private bool _captureMouse;
    private bool _captureKeyboard;
    
    private int _lastX;
    private int _lastY;
    private bool _firstMove = true;
    
    private IntPtr _mouseHookHandle = IntPtr.Zero;
    private IntPtr _keyboardHookHandle = IntPtr.Zero;
    private User32.HookProc? _mouseProc;
    private User32.HookProc? _keyboardProc;
    
    private Thread? _messagePumpThread;
    private uint _messagePumpThreadId;
    private CancellationTokenRegistration _startCancellationRegistration;
    


    public void Configure(bool captureMouse, bool captureKeyboard)
    {
        _captureMouse = captureMouse;
        _captureKeyboard = captureKeyboard;
    }



    public Task StartAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var startupTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        _messagePumpThread = new Thread(() =>
        {
            try
            {
                _messagePumpThreadId = Kernel32.GetCurrentThreadId();
                
                _mouseProc = MouseHookCallback;
                _keyboardProc = KeyboardHookCallback;

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
                    Error?.Invoke(this, $"Message pump error: {ex.Message}");
                }
            }
            finally
            {
                UninstallHooks();
                _messagePumpThreadId = 0;
            }
        });

        _messagePumpThread.IsBackground = true;
        _messagePumpThread.Start();
        
        _startCancellationRegistration.Dispose();
        _startCancellationRegistration = ct.Register(() =>
        {
            startupTcs.TrySetCanceled(ct);
            Stop();
        });

        return startupTcs.Task;
    }

    public void Stop()
    {
        _startCancellationRegistration.Dispose();

        if (_messagePumpThreadId != 0)
        {
            User32.PostThreadMessage(_messagePumpThreadId, User32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }
    }

    protected virtual IntPtr InstallMouseHook(IntPtr moduleHandle)
        => User32.SetWindowsHookEx(User32.WH_MOUSE_LL, _mouseProc!, moduleHandle, 0);

    protected virtual IntPtr InstallKeyboardHook(IntPtr moduleHandle)
        => User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, _keyboardProc!, moduleHandle, 0);

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
        Stop();
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
                    
                    int delta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                    value = delta; 
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

                    if (deltaX != 0)
                    {
                        var xArgs = new InputCaptureEventArgs
                        {
                            Type = InputEventType.MouseMove,
                            Code = InputEventCode.REL_X,
                            Value = deltaX,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            DeviceName = "VirtualMouse"
                        };
                        InputReceived?.Invoke(this, xArgs);
                    }

                    if (deltaY != 0)
                    {
                        var yArgs = new InputCaptureEventArgs
                        {
                            Type = InputEventType.MouseMove,
                            Code = InputEventCode.REL_Y,
                            Value = deltaY,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            DeviceName = "VirtualMouse"
                        };
                        InputReceived?.Invoke(this, yArgs);
                    }
                    
                    // Emit SYNC to flush the movement buffer in MacroRecorder
                    if (deltaX != 0 || deltaY != 0)
                    {
                        var syncArgs = new InputCaptureEventArgs
                        {
                            Type = InputEventType.Sync,
                            Code = 0,
                            Value = 0,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            DeviceName = "VirtualMouse"
                        };
                        InputReceived?.Invoke(this, syncArgs);
                    }
                    
                    evdevCode = 0; 
                    break;
            }

            if (evdevCode != 0)
            {
                // Mouse buttons should use MouseButton type, not Key
                var eventType = (type == InputEventCode.EV_KEY && evdevCode >= 272 && evdevCode <= 279)
                    ? InputEventType.MouseButton
                    : (type == InputEventCode.EV_REL && evdevCode == InputEventCode.REL_WHEEL) ? InputEventType.MouseScroll 
                    : (InputEventType)type;
                
                var args = new InputCaptureEventArgs
                {
                    Type = eventType,
                    Code = evdevCode,
                    Value = value,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    DeviceName = "VirtualMouse"
                };
                InputReceived?.Invoke(this, args);
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
            
            bool isDown = (msg == User32.WM_KEYDOWN || msg == User32.WM_SYSKEYDOWN);
            bool isUp = (msg == User32.WM_KEYUP || msg == User32.WM_SYSKEYUP);

            if (isDown || isUp)
            {
                int evdevCode = WindowsKeyMap.GetEvdevCode((ushort)hookStruct.vkCode);
                
                // Debug logging for key analysis
                if (isDown)
                {
                    Log.Debug("[WindowsInputCapture] KeyDown: VK={VK} (0x{VKHex}), Scan={Scan}, Flags={Flags}, Mapped={Evdev}", 
                        hookStruct.vkCode, hookStruct.vkCode.ToString("X"), hookStruct.scanCode, hookStruct.flags, evdevCode);
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
                
                if (evdevCode != 0)
                {
                    var args = new InputCaptureEventArgs
                    {
                        Type = (InputEventType)InputEventCode.EV_KEY,
                        Code = (ushort)evdevCode,
                        Value = isDown ? 1 : 0,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        DeviceName = "VirtualKeyboard"
                    };
                    InputReceived?.Invoke(this, args);
                }
                else if (isDown)
                {
                    Log.Warning("[WindowsInputCapture] Unmapped key: VK={VK} (0x{VKHex})", hookStruct.vkCode, hookStruct.vkCode.ToString("X"));
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
}
