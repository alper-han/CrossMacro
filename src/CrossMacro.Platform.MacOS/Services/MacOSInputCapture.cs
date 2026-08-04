
namespace CrossMacro.Platform.MacOS.Services;

public sealed class MacOSInputCapture : IInputCapture
{
    // Holds all native handles to keep raw IntPtr fields off the IDisposable outer class (CA2216).
    private sealed class CaptureNativeHandles
    {
        internal IntPtr EventTap;
        internal IntPtr SystemDefinedEventTap;
        internal IntPtr RunLoopSource;
        internal IntPtr SystemDefinedRunLoopSource;
        internal IntPtr RunLoop;
    }

    private readonly Lock _stateLock = new();
    private readonly CaptureNativeHandles _h = new();
    private Thread? _captureThread;
    private bool _captureMouse = true;
    private bool _captureKeyboard = true;
    private volatile bool _stopRequested;
    private bool _disposed;
    private CancellationTokenRegistration _startCancellationRegistration;
    private Task? _startupTask;
    private TaskCompletionSource<object?>? _startupCompletionSource;
    private readonly Func<bool> _requestListenEventAccess;

    private readonly CoreGraphics.CGEventTapCallBack _callbackDelegate;

    public string ProviderName => "macOS CoreGraphics";
    public bool IsSupported => OperatingSystem.IsMacOS();

    public event EventHandler<CapturedInputEventArgs>? InputReceived;
    public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

    public MacOSInputCapture()
        : this(MacOSPermissionChecker.RequestListenEventAccess) { /* Empty */ }

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

    public async Task StartAsync(CancellationToken ct)
    {
        await _startCancellationRegistration.DisposeAsync().ConfigureAwait(false);

        Task startupTask;
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!IsSupported)
            {
                CaptureError?.Invoke(this, new InputCaptureErrorEventArgs("Input capture is only supported on macOS."));
                return;
            }

            if (_captureThread is not null && _captureThread.IsAlive)
            {
                startupTask = _startupTask ?? Task.CompletedTask;
            }
            else
            {
                ct.ThrowIfCancellationRequested();
                _ = _requestListenEventAccess();

                _stopRequested = false;
                var startupCompletionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _startupCompletionSource = startupCompletionSource;
                _startupTask = startupCompletionSource.Task;

                _startCancellationRegistration = ct.Register(() => HandleStartCancellation(startupCompletionSource, ct));

                _captureThread = new Thread(() => CaptureLoop(startupCompletionSource))
                {
                    IsBackground = true,
                    Name = "MacOSInputCapture",
                };
                _captureThread.Start();
                startupTask = startupCompletionSource.Task;
            }
        }

        await startupTask.ConfigureAwait(false);
    }

    public void StopCapture()
    {
        _stopRequested = true;
        _startCancellationRegistration.Dispose();
        _ = (_startupCompletionSource?.TrySetCanceled(CancellationToken.None));
        RequestStop();

        var captureThread = _captureThread;
        if (captureThread is not null && captureThread.IsAlive && !ReferenceEquals(Thread.CurrentThread, captureThread))
        {
            _ = captureThread.Join(500);
        }
    }

    private void HandleStartCancellation(TaskCompletionSource<object?> startupCompletionSource, CancellationToken ct)
    {
        _stopRequested = true;
        _ = startupCompletionSource.TrySetCanceled(ct);
        RequestStop();
    }

    private void RequestStop()
    {
        if (_h.EventTap != IntPtr.Zero)
        {
            CoreGraphics.CGEventTapEnable(_h.EventTap, enable: false);
        }

        if (_h.SystemDefinedEventTap != IntPtr.Zero)
        {
            CoreGraphics.CGEventTapEnable(_h.SystemDefinedEventTap, enable: false);
        }

        if (_h.RunLoop != IntPtr.Zero)
        {
            CoreFoundation.CFRunLoopStop(_h.RunLoop);
        }
    }

    private void CaptureLoop(TaskCompletionSource<object?> startupCompletionSource)
    {
        try
        {
            _h.RunLoop = CoreFoundation.CFRunLoopGetCurrent();
            if (_stopRequested)
            {
                return;
            }

            TrySetupSystemDefinedTap();

            bool useSessionSystemDefinedTap = _h.SystemDefinedEventTap != IntPtr.Zero &&
                _h.SystemDefinedRunLoopSource != IntPtr.Zero;
            var eventsOfInterest = CreateHidEventMask(useSessionSystemDefinedTap);

            if (!TrySetupMainEventTap(startupCompletionSource, useSessionSystemDefinedTap, eventsOfInterest))
            {
                return;
            }

            if (_stopRequested)
            {
                return;
            }

            _ = startupCompletionSource.TrySetResult(null);
            CoreFoundation.CFRunLoopRun();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            FailStartup(startupCompletionSource, ex, $"Capture loop error: {ex.Message}");
        }
        finally
        {
            ReleaseCaptureHandles();
            lock (_stateLock)
            {
                if (ReferenceEquals(_captureThread, Thread.CurrentThread))
                {
                    _captureThread = null;
                }
            }
        }
    }

    private void TrySetupSystemDefinedTap()
    {
        _h.SystemDefinedEventTap = CoreGraphics.CGEventTapCreate(
            CoreGraphics.CGEventTapLocation.SessionEventTap,
            CoreGraphics.CGEventTapPlacement.HeadInsertEventTap,
            CreateObserveOnlyTapOptions(),
            CreateSystemDefinedEventMask(),
            Marshal.GetFunctionPointerForDelegate(_callbackDelegate),
            IntPtr.Zero
        );

        if (_h.SystemDefinedEventTap != IntPtr.Zero)
        {
            _h.SystemDefinedRunLoopSource = CoreFoundation.CFMachPortCreateRunLoopSource(IntPtr.Zero, _h.SystemDefinedEventTap, IntPtr.Zero);
            if (_h.SystemDefinedRunLoopSource == IntPtr.Zero)
            {
                CoreFoundation.CFRelease(_h.SystemDefinedEventTap);
                _h.SystemDefinedEventTap = IntPtr.Zero;
            }
        }
    }

    private bool TrySetupMainEventTap(
        TaskCompletionSource<object?> startupCompletionSource,
        bool useSessionSystemDefinedTap,
        ulong eventsOfInterest)
    {
        _h.EventTap = CoreGraphics.CGEventTapCreate(
            CoreGraphics.CGEventTapLocation.HIDEventTap,
            CoreGraphics.CGEventTapPlacement.HeadInsertEventTap,
            CreateObserveOnlyTapOptions(),
            eventsOfInterest,
            Marshal.GetFunctionPointerForDelegate(_callbackDelegate),
            IntPtr.Zero
        );

        if (_h.EventTap == IntPtr.Zero)
        {
            FailStartup(
                startupCompletionSource,
                new InvalidOperationException("Failed to create CGEventTap. Check Input Monitoring permission in System Settings."));
            return false;
        }

        if (useSessionSystemDefinedTap)
        {
            CoreFoundation.CFRunLoopAddSource(_h.RunLoop, _h.SystemDefinedRunLoopSource, CoreFoundation.kCFRunLoopCommonModes);
            CoreGraphics.CGEventTapEnable(_h.SystemDefinedEventTap, enable: true);
        }

        _h.RunLoopSource = CoreFoundation.CFMachPortCreateRunLoopSource(IntPtr.Zero, _h.EventTap, IntPtr.Zero);
        CoreFoundation.CFRunLoopAddSource(_h.RunLoop, _h.RunLoopSource, CoreFoundation.kCFRunLoopCommonModes);
        CoreGraphics.CGEventTapEnable(_h.EventTap, enable: true);
        return true;
    }

    private void ReleaseCaptureHandles()
    {
        if (_h.SystemDefinedRunLoopSource != IntPtr.Zero)
        {
            CoreFoundation.CFRelease(_h.SystemDefinedRunLoopSource);
        }

        if (_h.SystemDefinedEventTap != IntPtr.Zero)
        {
            CoreFoundation.CFRelease(_h.SystemDefinedEventTap);
        }

        if (_h.RunLoopSource != IntPtr.Zero)
        {
            CoreFoundation.CFRelease(_h.RunLoopSource);
        }

        if (_h.EventTap != IntPtr.Zero)
        {
            CoreFoundation.CFRelease(_h.EventTap);
        }

        _h.SystemDefinedRunLoopSource = IntPtr.Zero;
        _h.SystemDefinedEventTap = IntPtr.Zero;
        _h.RunLoopSource = IntPtr.Zero;
        _h.EventTap = IntPtr.Zero;
        _h.RunLoop = IntPtr.Zero;
    }

    private void FailStartup(
        TaskCompletionSource<object?> startupCompletionSource,
        Exception exception,
        string? errorMessage = null)
    {
        if (!startupCompletionSource.TrySetException(exception) &&
            !startupCompletionSource.Task.IsCanceled)
        {
            CaptureError?.Invoke(this, new InputCaptureErrorEventArgs(errorMessage ?? exception.Message));
        }
    }

    private IntPtr EventTapCallback(IntPtr proxy, CoreGraphics.CGEventType type, IntPtr eventRef, IntPtr userInfo)
    {
        try
        {
            if (type is CoreGraphics.CGEventType.TapDisabledByTimeout)
            {
                EnableActiveEventTaps();
                return eventRef;
            }

            if (type is CoreGraphics.CGEventType.TapDisabledByUserInput)
            {
                return eventRef;
            }

            ProcessAndFire(type, eventRef);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine($"[MacOSInputCapture] Error in callback: {ex}");
            try
            {
                CaptureError?.Invoke(this, new InputCaptureErrorEventArgs($"Error processing event: {ex.Message}"));
            }
            catch (Exception errorHandlerException) when (errorHandlerException is not OutOfMemoryException)
            {
                System.Diagnostics.Debug.WriteLine($"[MacOSInputCapture] Error handler threw: {errorHandlerException}");
            }
        }

        return eventRef;
    }

    private void EnableActiveEventTaps()
    {
        if (_h.EventTap != IntPtr.Zero)
        {
            CoreGraphics.CGEventTapEnable(_h.EventTap, enable: true);
        }

        if (_h.SystemDefinedEventTap != IntPtr.Zero)
        {
            CoreGraphics.CGEventTapEnable(_h.SystemDefinedEventTap, enable: true);
        }
    }

    private void ProcessAndFire(CoreGraphics.CGEventType type, IntPtr eventRef)
    {
        if (!_captureMouse && IsMouseEvent(type))
        {
            return;
        }

        if (!_captureKeyboard && IsKeyEvent(type))
        {
            return;
        }

        if (IsKeyEvent(type) &&
            ShouldIgnoreKeyboardEvent(CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventSourceUserData)))
        {
            return;
        }

        long timestamp = GetCurrentTimestamp();

        if (IsKeyEvent(type))
        {
            ProcessKeyEvent(type, eventRef, timestamp);
        }
        else if (IsMouseEvent(type))
        {
            ProcessMouseEvent(type, eventRef, timestamp);
        }
    }

    private void ProcessKeyEvent(CoreGraphics.CGEventType type, IntPtr eventRef, long timestamp)
    {
        if (type is CoreGraphics.CGEventType.SystemDefined)
        {
            long subtype = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventSubtype);
            long data1 = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventData1);

            if (!TryCreateSystemDefinedInput(type, subtype, data1, timestamp, out var systemDefinedEvent))
            {
                return;
            }

            InputReceived?.Invoke(this, new CapturedInputEventArgs(systemDefinedEvent));
            return;
        }

        long keyCodeNative = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.KeyboardEventKeycode);
        var flags = type is CoreGraphics.CGEventType.FlagsChanged
            ? CoreGraphics.CGEventGetFlags(eventRef)
            : default;

        if (!TryCreateKeyboardInput(type, (ushort)keyCodeNative, flags, timestamp, out var keyEvent))
        {
            return;
        }

        InputReceived?.Invoke(this, new CapturedInputEventArgs(keyEvent));
    }

    private void ProcessMouseEvent(CoreGraphics.CGEventType type, IntPtr eventRef, long timestamp)
    {
        if (type is CoreGraphics.CGEventType.LeftMouseDown or CoreGraphics.CGEventType.LeftMouseUp)
        {
            FireBtn(MouseButtonCode.Left, type is CoreGraphics.CGEventType.LeftMouseDown, timestamp);
        }
        else if (type is CoreGraphics.CGEventType.RightMouseDown or CoreGraphics.CGEventType.RightMouseUp)
        {
            FireBtn(MouseButtonCode.Right, type is CoreGraphics.CGEventType.RightMouseDown, timestamp);
        }
        else if (type is CoreGraphics.CGEventType.OtherMouseDown or CoreGraphics.CGEventType.OtherMouseUp)
        {
            long btnNum = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.MouseEventButtonNumber);
            if (TryMapOtherMouseButton(btnNum, out var button))
            {
                FireBtn(button, type is CoreGraphics.CGEventType.OtherMouseDown, timestamp);
            }
        }

        if (type is CoreGraphics.CGEventType.MouseMoved or CoreGraphics.CGEventType.LeftMouseDragged or CoreGraphics.CGEventType.RightMouseDragged or CoreGraphics.CGEventType.OtherMouseDragged)
        {
            var loc = CoreGraphics.CGEventGetLocation(eventRef);
            InputReceived?.Invoke(this, new CapturedInputEventArgs
            {
                Type = InputEventType.MouseMove,
                Code = InputEventCode.ABS_X,
                Value = (int)loc.X,
                Timestamp = timestamp,
            });
            InputReceived?.Invoke(this, new CapturedInputEventArgs
            {
                Type = InputEventType.MouseMove,
                Code = InputEventCode.ABS_Y,
                Value = (int)loc.Y,
                Timestamp = timestamp,
            });

            // SYNC event to ensure X and Y are processed together
            InputReceived?.Invoke(this, new CapturedInputEventArgs
            {
                Type = InputEventType.Sync,
                Code = 0,
                Value = 0,
                Timestamp = timestamp,
                DeviceName = ProviderName,
            });
        }

        if (type is CoreGraphics.CGEventType.ScrollWheel)
        {
            ProcessScrollWheelEvent(eventRef, timestamp);
        }
    }

    internal static bool TryMapOtherMouseButton(long buttonNumber, out int button)
    {
        button = buttonNumber switch
        {
            2 => MouseButtonCode.Middle,
            3 => MouseButtonCode.Side1,
            4 => MouseButtonCode.Side2,
            _ => 0,
        };
        return button is not 0;
    }

    private void ProcessScrollWheelEvent(IntPtr eventRef, long timestamp)
    {
        long dy = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.ScrollWheelEventDeltaAxis1);
        if (dy != 0)
        {
            InputReceived?.Invoke(this, new CapturedInputEventArgs
            {
                Type = InputEventType.MouseScroll,
                Code = InputEventCode.REL_WHEEL,
                Value = (int)dy,
                Timestamp = timestamp,
            });
        }
    }

    private void FireBtn(int btnCode, bool pressed, long timestamp)
    {
        InputReceived?.Invoke(this, new CapturedInputEventArgs
        {
            Type = InputEventType.MouseButton,
            Code = btnCode,
            Value = pressed ? 1 : 0,
            Timestamp = timestamp,
        });
    }

    internal static bool TryCreateKeyboardInput(
        CoreGraphics.CGEventType type,
        ushort nativeKeyCode,
        CoreGraphics.CGEventModifiers flags,
        long timestamp,
        out CapturedInputEvent inputEvent)
    {
        inputEvent = default;

        if (!KeyMap.TryFromMacKey(nativeKeyCode, out var code))
        {
            return false;
        }

        int value = 0;
        if (type is CoreGraphics.CGEventType.KeyDown)
        {
            value = 1;
        }
        else if (type is CoreGraphics.CGEventType.FlagsChanged)
        {
            bool isPressed = IsModifierPressed(code, flags);
            value = isPressed ? 1 : 0;
        }

        inputEvent = new CapturedInputEvent
        {
            Type = InputEventType.Key,
            Code = code,
            Value = value,
            Timestamp = timestamp,
        };

        return true;
    }

    internal static bool TryCreateSystemDefinedInput(
        CoreGraphics.CGEventType type,
        long subtype,
        long data1,
        long timestamp,
        out CapturedInputEvent inputEvent)
    {
        inputEvent = default;

        if (type is not CoreGraphics.CGEventType.SystemDefined || subtype != MacOSSystemKeyMap.NxSubtypeAuxControlButtons)
        {
            return false;
        }

        int valueState = (int)((data1 >> 8) & 0xFF);
        int value;
        if (valueState == MacOSSystemKeyMap.SystemDefinedKeyDownState)
        {
            value = 1;
        }
        else if (valueState == MacOSSystemKeyMap.SystemDefinedKeyUpState)
        {
            value = 0;
        }
        else
        {
            return false;
        }

        int keyType = (int)((data1 >> 16) & 0xFFFF);
        if (!MacOSSystemKeyMap.TryGetInputEventCode(keyType, out var code))
        {
            return false;
        }

        inputEvent = new CapturedInputEvent
        {
            Type = InputEventType.Key,
            Code = code,
            Value = value,
            Timestamp = timestamp,
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

    private static bool IsModifierPressed(int code, CoreGraphics.CGEventModifiers flags)
    {
        if (code is InputEventCode.KEY_LEFTSHIFT or InputEventCode.KEY_RIGHTSHIFT)
        {
            return flags.HasFlag(CoreGraphics.CGEventModifiers.Shift);
        }

        if (code is InputEventCode.KEY_LEFTCTRL or InputEventCode.KEY_RIGHTCTRL)
        {
            return flags.HasFlag(CoreGraphics.CGEventModifiers.Control);
        }

        if (code is InputEventCode.KEY_LEFTALT or InputEventCode.KEY_RIGHTALT)
        {
            return flags.HasFlag(CoreGraphics.CGEventModifiers.Alternate);
        }

        if (code is InputEventCode.KEY_LEFTMETA or InputEventCode.KEY_RIGHTMETA)
        {
            return flags.HasFlag(CoreGraphics.CGEventModifiers.Command);
        }

        if (code == InputEventCode.KEY_CAPSLOCK)
        {
            return flags.HasFlag(CoreGraphics.CGEventModifiers.AlphaShift);
        }

        return false;
    }

    private static bool IsMouseEvent(CoreGraphics.CGEventType type)
    {
        return type is not (CoreGraphics.CGEventType.KeyDown or CoreGraphics.CGEventType.KeyUp or CoreGraphics.CGEventType.FlagsChanged or CoreGraphics.CGEventType.SystemDefined);
    }

    private static bool IsKeyEvent(CoreGraphics.CGEventType type)
    {
        return type is CoreGraphics.CGEventType.KeyDown or CoreGraphics.CGEventType.KeyUp or CoreGraphics.CGEventType.FlagsChanged or CoreGraphics.CGEventType.SystemDefined;
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
        StopCapture();
        GC.SuppressFinalize(this);
    }
}
