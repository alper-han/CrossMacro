using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.MacOS.Helpers;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services;

public class MacOSInputCapture : IInputCapture
{
    private readonly Lock _stateLock = new();
    private IntPtr _eventTap;
    private IntPtr _systemDefinedEventTap;
    private IntPtr _runLoopSource;
    private IntPtr _systemDefinedRunLoopSource;
    private IntPtr _runLoop;
    private Thread? _captureThread;
    private bool _captureMouse = true;
    private bool _captureKeyboard = true;
    private volatile bool _stopRequested;
    private bool _disposed;
    private CancellationTokenRegistration _startCancellationRegistration;
    private Task? _startupTask;
    private TaskCompletionSource<object?>? _startupCompletionSource;
    private readonly Func<bool> _requestListenEventAccess;

    private CoreGraphics.CGEventTapCallBack _callbackDelegate;

    public string ProviderName => "macOS CoreGraphics";
    public bool IsSupported => OperatingSystem.IsMacOS();

    public event EventHandler<InputCaptureEventArgs>? InputReceived;
    public event EventHandler<string>? Error;

    public MacOSInputCapture()
        : this(MacOSPermissionChecker.RequestListenEventAccess)
    {
    }

    internal MacOSInputCapture(Func<bool> requestListenEventAccess)
    {
        _requestListenEventAccess = requestListenEventAccess ?? throw new ArgumentNullException(nameof(requestListenEventAccess));
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
                return _startupTask ?? Task.CompletedTask;
            }

            ct.ThrowIfCancellationRequested();
            _requestListenEventAccess();

            _stopRequested = false;
            var startupCompletionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _startupCompletionSource = startupCompletionSource;
            _startupTask = startupCompletionSource.Task;

            _startCancellationRegistration.Dispose();
            _startCancellationRegistration = ct.Register(() => HandleStartCancellation(startupCompletionSource));

            _captureThread = new Thread(() => CaptureLoop(startupCompletionSource))
            {
                IsBackground = true,
                Name = "MacOSInputCapture"
            };
            _captureThread.Start();
        }

        return _startupTask!;
    }

    public void Stop()
    {
        _stopRequested = true;
        _startCancellationRegistration.Dispose();
        _startupCompletionSource?.TrySetCanceled();
        RequestStop();

        var captureThread = _captureThread;
        if (captureThread != null &&
            captureThread.IsAlive &&
            !ReferenceEquals(Thread.CurrentThread, captureThread))
        {
            captureThread.Join(500);
        }
    }

    private void HandleStartCancellation(TaskCompletionSource<object?> startupCompletionSource)
    {
        _stopRequested = true;
        startupCompletionSource.TrySetCanceled();
        RequestStop();
    }

    private void RequestStop()
    {
        _startCancellationRegistration.Dispose();
        
        if (_eventTap != IntPtr.Zero)
        {
            CoreGraphics.CGEventTapEnable(_eventTap, false);
        }

        if (_systemDefinedEventTap != IntPtr.Zero)
        {
            CoreGraphics.CGEventTapEnable(_systemDefinedEventTap, false);
        }
        
        if (_runLoop != IntPtr.Zero)
        {
            CoreFoundation.CFRunLoopStop(_runLoop);
        }
    }

    private void CaptureLoop(TaskCompletionSource<object?> startupCompletionSource)
    {
        try
        {
            _runLoop = CoreFoundation.CFRunLoopGetCurrent();
            if (_stopRequested) return;

            _systemDefinedEventTap = CoreGraphics.CGEventTapCreate(
                CoreGraphics.CGEventTapLocation.SessionEventTap,
                CoreGraphics.CGEventTapPlacement.HeadInsertEventTap,
                CreateObserveOnlyTapOptions(),
                CreateSystemDefinedEventMask(),
                _callbackDelegate,
                IntPtr.Zero
            );

            if (_systemDefinedEventTap != IntPtr.Zero)
            {
                _systemDefinedRunLoopSource = CoreFoundation.CFMachPortCreateRunLoopSource(IntPtr.Zero, _systemDefinedEventTap, IntPtr.Zero);
                if (_systemDefinedRunLoopSource == IntPtr.Zero)
                {
                    CoreFoundation.CFRelease(_systemDefinedEventTap);
                    _systemDefinedEventTap = IntPtr.Zero;
                }
            }

            bool useSessionSystemDefinedTap = _systemDefinedEventTap != IntPtr.Zero &&
                _systemDefinedRunLoopSource != IntPtr.Zero;
            var eventsOfInterest = CreateHidEventMask(useSessionSystemDefinedTap);

            _eventTap = CoreGraphics.CGEventTapCreate(
                CoreGraphics.CGEventTapLocation.HIDEventTap,
                CoreGraphics.CGEventTapPlacement.HeadInsertEventTap,
                CreateObserveOnlyTapOptions(),
                eventsOfInterest,
                _callbackDelegate,
                IntPtr.Zero
            );

            if (_eventTap == IntPtr.Zero)
            {
                FailStartup(
                    startupCompletionSource,
                    new InvalidOperationException("Failed to create CGEventTap. Check Input Monitoring permission in System Settings."));
                return;
            }

            if (useSessionSystemDefinedTap)
            {
                CoreFoundation.CFRunLoopAddSource(_runLoop, _systemDefinedRunLoopSource, CoreFoundation.kCFRunLoopCommonModes);
                CoreGraphics.CGEventTapEnable(_systemDefinedEventTap, true);
            }

            _runLoopSource = CoreFoundation.CFMachPortCreateRunLoopSource(IntPtr.Zero, _eventTap, IntPtr.Zero);
            CoreFoundation.CFRunLoopAddSource(_runLoop, _runLoopSource, CoreFoundation.kCFRunLoopCommonModes);
            CoreGraphics.CGEventTapEnable(_eventTap, true);

            if (_stopRequested) return;
            startupCompletionSource.TrySetResult(null);

            CoreFoundation.CFRunLoopRun();
        }
        catch (Exception ex)
        {
            FailStartup(startupCompletionSource, ex, $"Capture loop error: {ex.Message}");
        }
        finally
        {
            if (_systemDefinedRunLoopSource != IntPtr.Zero) CoreFoundation.CFRelease(_systemDefinedRunLoopSource);
            if (_systemDefinedEventTap != IntPtr.Zero) CoreFoundation.CFRelease(_systemDefinedEventTap);
            if (_runLoopSource != IntPtr.Zero) CoreFoundation.CFRelease(_runLoopSource);
            if (_eventTap != IntPtr.Zero) CoreFoundation.CFRelease(_eventTap);

            _systemDefinedRunLoopSource = IntPtr.Zero;
            _systemDefinedEventTap = IntPtr.Zero;
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

    private void FailStartup(
        TaskCompletionSource<object?> startupCompletionSource,
        Exception exception,
        string? errorMessage = null)
    {
        if (!startupCompletionSource.TrySetException(exception) &&
            !startupCompletionSource.Task.IsCanceled)
        {
            Error?.Invoke(this, errorMessage ?? exception.Message);
        }
    }

    private IntPtr EventTapCallback(IntPtr proxy, CoreGraphics.CGEventType type, IntPtr eventRef, IntPtr userInfo)
    {
        try
        {
            if (type == CoreGraphics.CGEventType.TapDisabledByTimeout)
            {
                EnableActiveEventTaps();
                return eventRef;
            }

            if (type == CoreGraphics.CGEventType.TapDisabledByUserInput)
            {
                return eventRef;
            }

            ProcessAndFire(type, eventRef);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MacOSInputCapture] Error in callback: {ex}");
            try
            {
                Error?.Invoke(this, $"Error processing event: {ex.Message}");
            }
            catch (Exception errorHandlerException)
            {
                System.Diagnostics.Debug.WriteLine($"[MacOSInputCapture] Error handler threw: {errorHandlerException}");
            }
        }

        return eventRef;
    }

    private void EnableActiveEventTaps()
    {
        if (_eventTap != IntPtr.Zero)
        {
            CoreGraphics.CGEventTapEnable(_eventTap, true);
        }

        if (_systemDefinedEventTap != IntPtr.Zero)
        {
            CoreGraphics.CGEventTapEnable(_systemDefinedEventTap, true);
        }
    }

    private void ProcessAndFire(CoreGraphics.CGEventType type, IntPtr eventRef)
    {
        if (!_captureMouse && IsMouseEvent(type)) return;
        if (!_captureKeyboard && IsKeyEvent(type)) return;

        if (IsKeyEvent(type) &&
            ShouldIgnoreKeyboardEvent(CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventSourceUserData)))
        {
            return;
        }

        long timestamp = GetCurrentTimestamp();

        if (IsKeyEvent(type))
        {
            if (type == CoreGraphics.CGEventType.SystemDefined)
            {
                long subtype = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventSubtype);
                long data1 = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventData1);

                if (!TryCreateSystemDefinedInput(type, subtype, data1, timestamp, out var systemDefinedEvent))
                {
                    return;
                }

                InputReceived?.Invoke(this, systemDefinedEvent);
                return;
            }

            long keyCodeNative = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.KeyboardEventKeycode);
            var flags = type == CoreGraphics.CGEventType.FlagsChanged
                ? CoreGraphics.CGEventGetFlags(eventRef)
                : default;

            if (!TryCreateKeyboardInput(type, (ushort)keyCodeNative, flags, timestamp, out var keyEvent))
            {
                return;
            }

            InputReceived?.Invoke(this, keyEvent);
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

    internal static bool TryCreateKeyboardInput(
        CoreGraphics.CGEventType type,
        ushort nativeKeyCode,
        CoreGraphics.CGEventFlags flags,
        long timestamp,
        out InputCaptureEventArgs inputEvent)
    {
        inputEvent = default;

        if (!KeyMap.TryFromMacKey(nativeKeyCode, out var code))
        {
            return false;
        }

        int value = 0;
        if (type == CoreGraphics.CGEventType.KeyDown) value = 1;
        else if (type == CoreGraphics.CGEventType.FlagsChanged)
        {
            bool isPressed = IsModifierPressed(code, flags);
            value = isPressed ? 1 : 0;
        }

        inputEvent = new InputCaptureEventArgs
        {
            Type = InputEventType.Key,
            Code = code,
            Value = value,
            Timestamp = timestamp
        };

        return true;
    }

    internal static bool TryCreateSystemDefinedInput(
        CoreGraphics.CGEventType type,
        long subtype,
        long data1,
        long timestamp,
        out InputCaptureEventArgs inputEvent)
    {
        inputEvent = default;

        if (type != CoreGraphics.CGEventType.SystemDefined || subtype != MacOSSystemKeyMap.NxSubtypeAuxControlButtons)
        {
            return false;
        }

        int valueState = (int)((data1 >> 8) & 0xFF);
        int value;
        if (valueState == MacOSSystemKeyMap.SystemDefinedKeyDownState) value = 1;
        else if (valueState == MacOSSystemKeyMap.SystemDefinedKeyUpState) value = 0;
        else return false;

        int keyType = (int)((data1 >> 16) & 0xFFFF);
        if (!MacOSSystemKeyMap.TryGetInputEventCode(keyType, out var code)) return false;

        inputEvent = new InputCaptureEventArgs
        {
            Type = InputEventType.Key,
            Code = code,
            Value = value,
            Timestamp = timestamp
        };

        return true;
    }

    internal static ulong CreateHidEventMask(bool useSessionSystemDefinedTap)
    {
        var mask =
            EventMask(CoreGraphics.CGEventType.KeyDown) |
            EventMask(CoreGraphics.CGEventType.KeyUp) |
            EventMask(CoreGraphics.CGEventType.FlagsChanged) |
            EventMask(CoreGraphics.CGEventType.LeftMouseDown) |
            EventMask(CoreGraphics.CGEventType.LeftMouseUp) |
            EventMask(CoreGraphics.CGEventType.RightMouseDown) |
            EventMask(CoreGraphics.CGEventType.RightMouseUp) |
            EventMask(CoreGraphics.CGEventType.OtherMouseDown) |
            EventMask(CoreGraphics.CGEventType.OtherMouseUp) |
            EventMask(CoreGraphics.CGEventType.MouseMoved) |
            EventMask(CoreGraphics.CGEventType.LeftMouseDragged) |
            EventMask(CoreGraphics.CGEventType.RightMouseDragged) |
            EventMask(CoreGraphics.CGEventType.OtherMouseDragged) |
            EventMask(CoreGraphics.CGEventType.ScrollWheel);

        if (!useSessionSystemDefinedTap)
        {
            mask |= EventMask(CoreGraphics.CGEventType.SystemDefined);
        }

        return mask;
    }

    internal static ulong CreateSystemDefinedEventMask()
    {
        return EventMask(CoreGraphics.CGEventType.SystemDefined);
    }

    internal static CoreGraphics.CGEventTapOptions CreateObserveOnlyTapOptions()
    {
        return CoreGraphics.CGEventTapOptions.ListenOnly;
    }

    private static ulong EventMask(CoreGraphics.CGEventType type)
    {
        return 1UL << (int)type;
    }

    private static bool IsModifierPressed(int code, CoreGraphics.CGEventFlags flags)
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
               type != CoreGraphics.CGEventType.FlagsChanged &&
               type != CoreGraphics.CGEventType.SystemDefined;
    }

    private bool IsKeyEvent(CoreGraphics.CGEventType type)
    {
        return type == CoreGraphics.CGEventType.KeyDown ||
               type == CoreGraphics.CGEventType.KeyUp ||
               type == CoreGraphics.CGEventType.FlagsChanged ||
               type == CoreGraphics.CGEventType.SystemDefined;
    }

    internal static bool ShouldIgnoreKeyboardEvent(long eventSourceUserData)
    {
        return eventSourceUserData == InputEventMarkers.TextExpansionKeyboardEvent;
    }

    internal static long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
