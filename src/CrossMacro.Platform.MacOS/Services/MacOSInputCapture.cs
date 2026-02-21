using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services;

public class MacOSInputCapture : IInputCapture
{
    private readonly Lock _stateLock = new();
    private IntPtr _eventTap;
    private IntPtr _runLoopSource;
    private IntPtr _runLoop;
    private Thread? _captureThread;
    private bool _captureMouse = true;
    private bool _captureKeyboard = true;
    private volatile bool _stopRequested;
    private bool _disposed;
    private CancellationTokenRegistration _startCancellationRegistration;
    
    private CoreGraphics.CGEventTapCallBack _callbackDelegate;

    public string ProviderName => "macOS CoreGraphics";
    public bool IsSupported => OperatingSystem.IsMacOS();

    public event EventHandler<InputCaptureEventArgs>? InputReceived;
    public event EventHandler<string>? Error;

    public MacOSInputCapture()
    {
        _callbackDelegate = EventTapCallback;
    }

    public void Configure(bool captureMouse, bool captureKeyboard)
    {
        _captureMouse = captureMouse;
        _captureKeyboard = captureKeyboard;
    }



    public Task StartAsync(CancellationToken ct)
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MacOSInputCapture));
            }

            if (!IsSupported)
            {
                Error?.Invoke(this, "Input capture is only supported on macOS.");
                return Task.CompletedTask;
            }

            if (_captureThread != null && _captureThread.IsAlive)
            {
                return Task.CompletedTask;
            }

            _stopRequested = false;
            _startCancellationRegistration.Dispose();
            _startCancellationRegistration = ct.Register(Stop);

            _captureThread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Name = "MacOSInputCapture"
            };
            _captureThread.Start();
        }

        return Task.CompletedTask;
    }

    public void Stop()
    {
        _stopRequested = true;
        _startCancellationRegistration.Dispose();
        
        if (_eventTap != IntPtr.Zero)
        {
            CoreGraphics.CGEventTapEnable(_eventTap, false);
        }
        
        if (_runLoop != IntPtr.Zero)
        {
            CoreFoundation.CFRunLoopStop(_runLoop);
        }

        var captureThread = _captureThread;
        if (captureThread != null &&
            captureThread.IsAlive &&
            !ReferenceEquals(Thread.CurrentThread, captureThread))
        {
            captureThread.Join(500);
        }
    }

    private void CaptureLoop()
    {
        try
        {
            _runLoop = CoreFoundation.CFRunLoopGetCurrent();
            if (_stopRequested) return;

            var eventsOfInterest = (ulong)(
                (1 << (int)CoreGraphics.CGEventType.KeyDown) |
                (1 << (int)CoreGraphics.CGEventType.KeyUp) |
                (1 << (int)CoreGraphics.CGEventType.FlagsChanged) |
                (1 << (int)CoreGraphics.CGEventType.LeftMouseDown) |
                (1 << (int)CoreGraphics.CGEventType.LeftMouseUp) |
                (1 << (int)CoreGraphics.CGEventType.RightMouseDown) |
                (1 << (int)CoreGraphics.CGEventType.RightMouseUp) |
                (1 << (int)CoreGraphics.CGEventType.OtherMouseDown) |
                (1 << (int)CoreGraphics.CGEventType.OtherMouseUp) |
                (1 << (int)CoreGraphics.CGEventType.MouseMoved) |
                (1 << (int)CoreGraphics.CGEventType.LeftMouseDragged) |
                (1 << (int)CoreGraphics.CGEventType.RightMouseDragged) |
                (1 << (int)CoreGraphics.CGEventType.OtherMouseDragged) |
                (1 << (int)CoreGraphics.CGEventType.ScrollWheel)
            );

            _eventTap = CoreGraphics.CGEventTapCreate(
                CoreGraphics.CGEventTapLocation.HIDEventTap,
                CoreGraphics.CGEventTapPlacement.HeadInsertEventTap,
                CoreGraphics.CGEventTapOptions.Default,
                eventsOfInterest,
                _callbackDelegate,
                IntPtr.Zero
            );

            if (_eventTap == IntPtr.Zero)
            {
                Error?.Invoke(this, "Failed to create CGEventTap. Check Accessibility permissions.");
                return;
            }

            _runLoopSource = CoreFoundation.CFMachPortCreateRunLoopSource(IntPtr.Zero, _eventTap, IntPtr.Zero);
            CoreFoundation.CFRunLoopAddSource(_runLoop, _runLoopSource, CoreFoundation.kCFRunLoopCommonModes);

            CoreGraphics.CGEventTapEnable(_eventTap, true);

            if (_stopRequested) return;

            CoreFoundation.CFRunLoopRun();
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, $"Capture loop error: {ex.Message}");
        }
        finally
        {
            if (_runLoopSource != IntPtr.Zero) CoreFoundation.CFRelease(_runLoopSource);
            if (_eventTap != IntPtr.Zero) CoreFoundation.CFRelease(_eventTap);

            _runLoopSource = IntPtr.Zero;
            _eventTap = IntPtr.Zero;
            _runLoop = IntPtr.Zero;

            lock (_stateLock)
            {
                if (ReferenceEquals(_captureThread, Thread.CurrentThread))
                {
                    _captureThread = null;
                }
            }
        }
    }

    private IntPtr EventTapCallback(IntPtr proxy, CoreGraphics.CGEventType type, IntPtr eventRef, IntPtr userInfo)
    {
        if (type == CoreGraphics.CGEventType.TapDisabledByTimeout)
        {
            CoreGraphics.CGEventTapEnable(_eventTap, true);
            return eventRef;
        }
        
        if (type == CoreGraphics.CGEventType.TapDisabledByUserInput)
        {
            return eventRef;
        }

        try
        {
            ProcessAndFire(type, eventRef);
        }
        catch (Exception ex)
        {
             System.Diagnostics.Debug.WriteLine($"[MacOSInputCapture] Error in callback: {ex}");
             Error?.Invoke(this, $"Error processing event: {ex.Message}");
        }

        return eventRef;
    }

    private void ProcessAndFire(CoreGraphics.CGEventType type, IntPtr eventRef)
    {
         if (!_captureMouse && IsMouseEvent(type)) return;
         if (!_captureKeyboard && IsKeyEvent(type)) return;

         long timestamp = DateTime.UtcNow.Ticks;

         if (IsKeyEvent(type))
         {
            long keyCodeNative = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.KeyboardEventKeycode);
            int code = KeyMap.FromMacKey((ushort)keyCodeNative);
            int value = 0;
            
            if (type == CoreGraphics.CGEventType.KeyDown) value = 1;
            else if (type == CoreGraphics.CGEventType.KeyUp) value = 0;
            else if (type == CoreGraphics.CGEventType.FlagsChanged)
            {
                var flags = CoreGraphics.CGEventGetFlags(eventRef);
                bool isPressed = IsModifierPressed(code, flags);
                value = isPressed ? 1 : 0;
            }
            
            InputReceived?.Invoke(this, new InputCaptureEventArgs { 
                Type = InputEventType.Key, 
                Code = code, 
                Value = value, 
                Timestamp = timestamp 
            });
         }
         else if (IsMouseEvent(type))
         {
             if (type == CoreGraphics.CGEventType.LeftMouseDown || type == CoreGraphics.CGEventType.LeftMouseUp)
             {
                 FireBtn(MouseButtonCode.Left, type == CoreGraphics.CGEventType.LeftMouseDown, timestamp);
             }
             else if (type == CoreGraphics.CGEventType.RightMouseDown || type == CoreGraphics.CGEventType.RightMouseUp)
             {
                 FireBtn(MouseButtonCode.Right, type == CoreGraphics.CGEventType.RightMouseDown, timestamp);
             }
             else if (type == CoreGraphics.CGEventType.OtherMouseDown || type == CoreGraphics.CGEventType.OtherMouseUp)
             {
                 long btnNum = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.MouseEventButtonNumber);
                 if (btnNum == 2) FireBtn(MouseButtonCode.Middle, type == CoreGraphics.CGEventType.OtherMouseDown, timestamp);
             }
             
             if (type == CoreGraphics.CGEventType.MouseMoved || type == CoreGraphics.CGEventType.LeftMouseDragged || 
                 type == CoreGraphics.CGEventType.RightMouseDragged || type == CoreGraphics.CGEventType.OtherMouseDragged)
             {
                 var loc = CoreGraphics.CGEventGetLocation(eventRef);
                 InputReceived?.Invoke(this, new InputCaptureEventArgs { 
                    Type = InputEventType.MouseMove, 
                    Code = InputEventCode.ABS_X, 
                    Value = (int)loc.X, 
                    Timestamp = timestamp 
                 });
                 InputReceived?.Invoke(this, new InputCaptureEventArgs { 
                    Type = InputEventType.MouseMove, 
                    Code = InputEventCode.ABS_Y, 
                    Value = (int)loc.Y, 
                    Timestamp = timestamp 
                 });
                 
                 // SYNC event to ensure X and Y are processed together
                 InputReceived?.Invoke(this, new InputCaptureEventArgs { 
                    Type = InputEventType.Sync, 
                    Code = 0,
                    Value = 0,
                    Timestamp = timestamp,
                    DeviceName = ProviderName
                 });
             }
             
             if (type == CoreGraphics.CGEventType.ScrollWheel)
             {
                  long dy = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.ScrollWheelEventDeltaAxis1);
                  if (dy != 0)
                  {
                      InputReceived?.Invoke(this, new InputCaptureEventArgs {
                          Type = InputEventType.MouseScroll, 
                          Code = InputEventCode.REL_WHEEL,
                          Value = (int)dy, 
                          Timestamp = timestamp
                      });
                  }
             }
         }
    }

    private void FireBtn(int btnCode, bool pressed, long timestamp)
    {
        InputReceived?.Invoke(this, new InputCaptureEventArgs {
            Type = InputEventType.MouseButton,
            Code = btnCode,
            Value = pressed ? 1 : 0,
            Timestamp = timestamp
        });
    }

    private bool IsModifierPressed(int code, CoreGraphics.CGEventFlags flags)
    {
        if (code == InputEventCode.KEY_LEFTSHIFT || code == InputEventCode.KEY_RIGHTSHIFT) return flags.HasFlag(CoreGraphics.CGEventFlags.Shift);
        if (code == InputEventCode.KEY_LEFTCTRL || code == InputEventCode.KEY_RIGHTCTRL) return flags.HasFlag(CoreGraphics.CGEventFlags.Control);
        if (code == InputEventCode.KEY_LEFTALT || code == InputEventCode.KEY_RIGHTALT) return flags.HasFlag(CoreGraphics.CGEventFlags.Alternate);
        if (code == InputEventCode.KEY_LEFTMETA || code == InputEventCode.KEY_RIGHTMETA) return flags.HasFlag(CoreGraphics.CGEventFlags.Command);
        if (code == InputEventCode.KEY_CAPSLOCK) return flags.HasFlag(CoreGraphics.CGEventFlags.AlphaShift);
        return false;
    }

    private bool IsMouseEvent(CoreGraphics.CGEventType type)
    {
        return type != CoreGraphics.CGEventType.KeyDown &&
               type != CoreGraphics.CGEventType.KeyUp &&
               type != CoreGraphics.CGEventType.FlagsChanged;
    }

    private bool IsKeyEvent(CoreGraphics.CGEventType type)
    {
        return type == CoreGraphics.CGEventType.KeyDown ||
               type == CoreGraphics.CGEventType.KeyUp ||
               type == CoreGraphics.CGEventType.FlagsChanged;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }
}
