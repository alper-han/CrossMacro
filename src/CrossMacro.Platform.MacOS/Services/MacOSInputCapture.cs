
namespace CrossMacro.Platform.MacOS.Services;

public sealed class MacOSInputCapture : IInputCapture, IMouseCoordinateModeInputCapture
{
    private const double RunLoopSliceSeconds = 0.1;
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
    private bool _useRawRelativeCoordinates;
    private volatile bool _stopRequested;
    private bool _disposed;
    private CancellationTokenRegistration _startCancellationRegistration;
    private Task? _startupTask;
    private TaskCompletionSource<object?>? _startupCompletionSource;
    private MacOSInputEventDispatcher? _inputDispatcher;
    private int _dispatchOverflowSignaled;
    private readonly Func<bool> _requestListenEventAccess;
    private readonly Func<bool> _isMacOS;
    private readonly IMacOSInputCaptureNative _native;
    private readonly Action? _beforeRunLoop;

    private readonly CoreGraphics.CGEventTapCallBack _callbackDelegate;

    public string ProviderName => "macOS CoreGraphics";
    public bool IsSupported => _isMacOS();

    public event EventHandler<CapturedInputEventArgs>? InputReceived;
    public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

    public MacOSInputCapture()
        : this(MacOSPermissionChecker.RequestListenEventAccess) { /* Empty */ }

    internal MacOSInputCapture(
        Func<bool> requestListenEventAccess,
        IMacOSInputCaptureNative? native = null,
        Func<bool>? isMacOS = null,
        Action? beforeRunLoop = null)
    {
        _requestListenEventAccess = requestListenEventAccess ?? throw new ArgumentNullException(nameof(requestListenEventAccess));
        _native = native ?? new MacOSInputCaptureNative();
        _isMacOS = isMacOS ?? OperatingSystem.IsMacOS;
        _beforeRunLoop = beforeRunLoop;
        _callbackDelegate = EventTapCallback;
    }

    public void Configure(bool captureMouse, bool captureKeyboard)
    {
        _captureMouse = captureMouse;
        _captureKeyboard = captureKeyboard;
    }

    public void ConfigureCoordinateMode(bool useAbsoluteCoordinates, bool useLogicalCoordinates)
    {
        _useRawRelativeCoordinates = !useAbsoluteCoordinates && !useLogicalCoordinates;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        Task startupTask;
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!IsSupported)
            {
                CaptureError?.Invoke(this, new InputCaptureErrorEventArgs("Input capture is only supported on macOS."));
                return;
            }

            ct.ThrowIfCancellationRequested();

            if (_captureThread is not null && _captureThread.IsAlive)
            {
                startupTask = _startupTask ?? Task.CompletedTask;
            }
            else
            {
                _ = _requestListenEventAccess();

                if (_inputDispatcher is not null)
                {
                    _inputDispatcher.Dispose();
                    if (!_inputDispatcher.IsCompleted)
                    {
                        throw new InvalidOperationException(
                            "The previous macOS input event dispatcher has not stopped.");
                    }
                }

                _inputDispatcher = new MacOSInputEventDispatcher(DispatchInputEvent, ReportDispatchError);
                _dispatchOverflowSignaled = 0;

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

        await startupTask.WaitAsync(ct).ConfigureAwait(false);
    }

    public void StopCapture()
    {
        _stopRequested = true;
        _startCancellationRegistration.Dispose();
        _ = (_startupCompletionSource?.TrySetCanceled(CancellationToken.None));
        RequestStop();

        Thread? captureThread;
        lock (_stateLock)
        {
            captureThread = _captureThread;
        }

        if (captureThread is not null && captureThread.IsAlive && !ReferenceEquals(Thread.CurrentThread, captureThread))
        {
            _ = captureThread.Join(500);
        }

        _inputDispatcher?.Dispose();
    }

    private void HandleStartCancellation(TaskCompletionSource<object?> startupCompletionSource, CancellationToken ct)
    {
        _stopRequested = true;
        _ = startupCompletionSource.TrySetCanceled(ct);
        RequestStop();
    }

    private void RequestStop()
    {
        lock (_stateLock)
        {
            if (_h.EventTap != IntPtr.Zero)
            {
                _native.EnableEventTap(_h.EventTap, enable: false);
            }

            if (_h.SystemDefinedEventTap != IntPtr.Zero)
            {
                _native.EnableEventTap(_h.SystemDefinedEventTap, enable: false);
            }

            if (_h.RunLoop != IntPtr.Zero)
            {
                _native.StopRunLoop(_h.RunLoop);
            }
        }
    }

    private void CaptureLoop(TaskCompletionSource<object?> startupCompletionSource)
    {
        try
        {
            lock (_stateLock)
            {
                _h.RunLoop = _native.GetCurrentRunLoop();
                if (_h.RunLoop == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to obtain the Core Foundation capture run loop.");
                }

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
            }

            _beforeRunLoop?.Invoke();
            while (!_stopRequested)
            {
                _native.RunLoopOnce(RunLoopSliceSeconds);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            FailStartup(startupCompletionSource, ex, $"Capture loop error: {ex.Message}");
        }
        finally
        {
            _startCancellationRegistration.Dispose();
            ReleaseCaptureHandles();
            _inputDispatcher?.Dispose();
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
        _h.SystemDefinedEventTap = _native.CreateEventTap(
            CoreGraphics.CGEventTapLocation.SessionEventTap,
            CoreGraphics.CGEventTapPlacement.HeadInsertEventTap,
            CreateObserveOnlyTapOptions(),
            CreateSystemDefinedEventMask(),
            Marshal.GetFunctionPointerForDelegate(_callbackDelegate));

        if (_h.SystemDefinedEventTap != IntPtr.Zero)
        {
            _h.SystemDefinedRunLoopSource = _native.CreateRunLoopSource(_h.SystemDefinedEventTap);
            if (_h.SystemDefinedRunLoopSource == IntPtr.Zero)
            {
                _native.Release(_h.SystemDefinedEventTap);
                _h.SystemDefinedEventTap = IntPtr.Zero;
            }
        }
    }

    private bool TrySetupMainEventTap(
        TaskCompletionSource<object?> startupCompletionSource,
        bool useSessionSystemDefinedTap,
        ulong eventsOfInterest)
    {
        _h.EventTap = _native.CreateEventTap(
            CoreGraphics.CGEventTapLocation.HIDEventTap,
            CoreGraphics.CGEventTapPlacement.HeadInsertEventTap,
            CreateObserveOnlyTapOptions(),
            eventsOfInterest,
            Marshal.GetFunctionPointerForDelegate(_callbackDelegate));

        if (_h.EventTap == IntPtr.Zero)
        {
            FailStartup(
                startupCompletionSource,
                new InvalidOperationException("Failed to create CGEventTap. Check Input Monitoring permission in System Settings."));
            return false;
        }

        if (useSessionSystemDefinedTap)
        {
            _native.AddRunLoopSource(_h.RunLoop, _h.SystemDefinedRunLoopSource);
            _native.EnableEventTap(_h.SystemDefinedEventTap, enable: true);
        }

        _h.RunLoopSource = _native.CreateRunLoopSource(_h.EventTap);
        if (_h.RunLoopSource == IntPtr.Zero)
        {
            FailStartup(
                startupCompletionSource,
                new InvalidOperationException("Failed to create the main CGEventTap run-loop source."));
            return false;
        }

        _native.AddRunLoopSource(_h.RunLoop, _h.RunLoopSource);
        _native.EnableEventTap(_h.EventTap, enable: true);
        return true;
    }

    private void ReleaseCaptureHandles()
    {
        lock (_stateLock)
        {
            if (_h.SystemDefinedRunLoopSource != IntPtr.Zero)
            {
                _native.Release(_h.SystemDefinedRunLoopSource);
            }

            if (_h.SystemDefinedEventTap != IntPtr.Zero)
            {
                _native.Release(_h.SystemDefinedEventTap);
            }

            if (_h.RunLoopSource != IntPtr.Zero)
            {
                _native.Release(_h.RunLoopSource);
            }

            if (_h.EventTap != IntPtr.Zero)
            {
                _native.Release(_h.EventTap);
            }

            _h.SystemDefinedRunLoopSource = IntPtr.Zero;
            _h.SystemDefinedEventTap = IntPtr.Zero;
            _h.RunLoopSource = IntPtr.Zero;
            _h.EventTap = IntPtr.Zero;
            _h.RunLoop = IntPtr.Zero;
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
            CaptureError?.Invoke(this, new InputCaptureErrorEventArgs(errorMessage ?? exception.Message));
        }
    }

    private IntPtr EventTapCallback(IntPtr proxy, CoreGraphics.CGEventType type, IntPtr eventRef, IntPtr userInfo)
    {
        try
        {
            if (ShouldReenableEventTap(type))
            {
                EnableActiveEventTaps();
                return eventRef;
            }

            ProcessAndQueue(type, eventRef);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine($"[MacOSInputCapture] Error in callback: {ex}");
            QueueCaptureError($"Error processing event: {ex.Message}");
        }

        return eventRef;
    }

    private void EnableActiveEventTaps()
    {
        lock (_stateLock)
        {
            if (_h.EventTap != IntPtr.Zero)
            {
                _native.EnableEventTap(_h.EventTap, enable: true);
            }

            if (_h.SystemDefinedEventTap != IntPtr.Zero)
            {
                _native.EnableEventTap(_h.SystemDefinedEventTap, enable: true);
            }
        }
    }

    private void ProcessAndQueue(CoreGraphics.CGEventType type, IntPtr eventRef)
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
        long timestampMicroseconds = ResolveEventTimestampMicroseconds(
            CoreGraphics.CGEventGetTimestamp(eventRef),
            GetMonotonicTimestampMicroseconds());

        if (IsKeyEvent(type))
        {
            ProcessKeyEvent(type, eventRef, timestamp, timestampMicroseconds);
        }
        else if (IsMouseEvent(type))
        {
            ProcessMouseEvent(type, eventRef, timestamp, timestampMicroseconds);
        }
    }

    private void ProcessKeyEvent(
        CoreGraphics.CGEventType type,
        IntPtr eventRef,
        long timestamp,
        long timestampMicroseconds)
    {
        if (type is CoreGraphics.CGEventType.SystemDefined)
        {
            long subtype = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventSubtype);
            long data1 = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.EventData1);

            if (!TryCreateSystemDefinedInput(type, subtype, data1, timestamp, timestampMicroseconds, out var systemDefinedEvent))
            {
                return;
            }

            QueueInput(new CapturedInputEventArgs(systemDefinedEvent));
            return;
        }

        long keyCodeNative = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.KeyboardEventKeycode);
        var flags = type is CoreGraphics.CGEventType.FlagsChanged
            ? CoreGraphics.CGEventGetFlags(eventRef)
            : default;

        if (!TryCreateKeyboardInput(type, (ushort)keyCodeNative, flags, timestamp, timestampMicroseconds, out var keyEvent))
        {
            return;
        }

        QueueInput(new CapturedInputEventArgs(keyEvent));
    }

    private void ProcessMouseEvent(
        CoreGraphics.CGEventType type,
        IntPtr eventRef,
        long timestamp,
        long timestampMicroseconds)
    {
        if (type is CoreGraphics.CGEventType.LeftMouseDown or CoreGraphics.CGEventType.LeftMouseUp)
        {
            FireBtn(MouseButtonCode.Left, type is CoreGraphics.CGEventType.LeftMouseDown, timestamp, timestampMicroseconds);
        }
        else if (type is CoreGraphics.CGEventType.RightMouseDown or CoreGraphics.CGEventType.RightMouseUp)
        {
            FireBtn(MouseButtonCode.Right, type is CoreGraphics.CGEventType.RightMouseDown, timestamp, timestampMicroseconds);
        }
        else if (type is CoreGraphics.CGEventType.OtherMouseDown or CoreGraphics.CGEventType.OtherMouseUp)
        {
            long btnNum = CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.MouseEventButtonNumber);
            if (TryMapOtherMouseButton(btnNum, out var button))
            {
                FireBtn(button, type is CoreGraphics.CGEventType.OtherMouseDown, timestamp, timestampMicroseconds);
            }
        }

        if (type is CoreGraphics.CGEventType.MouseMoved or CoreGraphics.CGEventType.LeftMouseDragged or CoreGraphics.CGEventType.RightMouseDragged or CoreGraphics.CGEventType.OtherMouseDragged)
        {
            EmitMouseMovement(eventRef, timestamp, timestampMicroseconds);

            // SYNC event to ensure X and Y are processed together
            QueueInput(new CapturedInputEventArgs
            {
                Type = InputEventType.Sync,
                Code = 0,
                Value = 0,
                Timestamp = timestamp,
                TimestampMicroseconds = timestampMicroseconds,
                DeviceName = ProviderName,
            });
        }

        if (type is CoreGraphics.CGEventType.ScrollWheel)
        {
            ProcessScrollWheelEvent(eventRef, timestamp, timestampMicroseconds);
        }
    }

    private void EmitMouseMovement(IntPtr eventRef, long timestamp, long timestampMicroseconds)
    {
        if (_useRawRelativeCoordinates)
        {
            EmitMouseMoveAxis(
                InputEventCode.REL_X,
                CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.MouseEventDeltaX),
                timestamp,
                timestampMicroseconds);
            EmitMouseMoveAxis(
                InputEventCode.REL_Y,
                CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.MouseEventDeltaY),
                timestamp,
                timestampMicroseconds);
            return;
        }

        var location = CoreGraphics.CGEventGetLocation(eventRef);
        EmitMouseMoveAxis(InputEventCode.ABS_X, (long)location.X, timestamp, timestampMicroseconds);
        EmitMouseMoveAxis(InputEventCode.ABS_Y, (long)location.Y, timestamp, timestampMicroseconds);
    }

    private void EmitMouseMoveAxis(ushort code, long value, long timestamp, long timestampMicroseconds)
    {
        if (value is 0 && _useRawRelativeCoordinates)
        {
            return;
        }

        QueueInput(new CapturedInputEventArgs
        {
            Type = InputEventType.MouseMove,
            Code = code,
            Value = (int)Math.Clamp(value, int.MinValue, int.MaxValue),
            Timestamp = timestamp,
            TimestampMicroseconds = timestampMicroseconds,
        });
    }

    internal static bool TryMapOtherMouseButton(long buttonNumber, out int button)
        => MacOSInputEventPolicy.TryMapOtherMouseButton(buttonNumber, out button);

    private void ProcessScrollWheelEvent(IntPtr eventRef, long timestamp, long timestampMicroseconds)
    {
        bool isContinuous = CoreGraphics.CGEventGetIntegerValueField(
            eventRef,
            CoreGraphics.CGEventField.ScrollWheelEventIsContinuous) is not 0;
        EmitScrollAxis(
            InputEventCode.REL_WHEEL,
            ResolveScrollDelta(
                isContinuous,
                CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.ScrollWheelEventDeltaAxis1),
                CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.ScrollWheelEventPointDeltaAxis1),
                CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.ScrollWheelEventFixedPtDeltaAxis1)),
            timestamp,
            timestampMicroseconds);
        EmitScrollAxis(
            InputEventCode.REL_HWHEEL,
            ResolveScrollDelta(
                isContinuous,
                CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.ScrollWheelEventDeltaAxis2),
                CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.ScrollWheelEventPointDeltaAxis2),
                CoreGraphics.CGEventGetIntegerValueField(eventRef, CoreGraphics.CGEventField.ScrollWheelEventFixedPtDeltaAxis2)),
            timestamp,
            timestampMicroseconds);
    }

    private void EmitScrollAxis(ushort code, long value, long timestamp, long timestampMicroseconds)
    {
        if (TryCreateScrollInput(code, value, timestamp, timestampMicroseconds, out var scrollEvent))
        {
            QueueInput(new CapturedInputEventArgs(scrollEvent));
        }
    }

    private void FireBtn(int btnCode, bool pressed, long timestamp, long timestampMicroseconds)
    {
        QueueInput(new CapturedInputEventArgs
        {
            Type = InputEventType.MouseButton,
            Code = btnCode,
            Value = pressed ? 1 : 0,
            Timestamp = timestamp,
            TimestampMicroseconds = timestampMicroseconds,
        });
    }

    internal static bool TryCreateKeyboardInput(
        CoreGraphics.CGEventType type,
        ushort nativeKeyCode,
        CoreGraphics.CGEventModifiers flags,
        long timestamp,
        out CapturedInputEvent inputEvent)
        => TryCreateKeyboardInput(type, nativeKeyCode, flags, timestamp, timestampMicroseconds: 0, out inputEvent);

    internal static bool TryCreateKeyboardInput(
        CoreGraphics.CGEventType type,
        ushort nativeKeyCode,
        CoreGraphics.CGEventModifiers flags,
        long timestamp,
        long timestampMicroseconds,
        out CapturedInputEvent inputEvent)
        => MacOSInputEventPolicy.TryCreateKeyboardInput(type, nativeKeyCode, flags, timestamp, timestampMicroseconds, out inputEvent);

    internal static bool TryCreateSystemDefinedInput(
        CoreGraphics.CGEventType type,
        long subtype,
        long data1,
        long timestamp,
        out CapturedInputEvent inputEvent)
        => TryCreateSystemDefinedInput(type, subtype, data1, timestamp, timestampMicroseconds: 0, out inputEvent);

    internal static bool TryCreateSystemDefinedInput(
        CoreGraphics.CGEventType type,
        long subtype,
        long data1,
        long timestamp,
        long timestampMicroseconds,
        out CapturedInputEvent inputEvent)
        => MacOSInputEventPolicy.TryCreateSystemDefinedInput(type, subtype, data1, timestamp, timestampMicroseconds, out inputEvent);

    internal static bool TryCreateScrollInput(
        ushort code,
        long value,
        long timestamp,
        long timestampMicroseconds,
        out CapturedInputEvent inputEvent)
        => MacOSInputEventPolicy.TryCreateScrollInput(code, value, timestamp, timestampMicroseconds, out inputEvent);

    internal static long ResolveScrollDelta(
        bool isContinuous,
        long lineDelta,
        long pointDelta,
        long fixedPointDelta)
        => MacOSInputEventPolicy.ResolveScrollDelta(isContinuous, lineDelta, pointDelta, fixedPointDelta);

    internal static ulong CreateHidEventMask(bool useSessionSystemDefinedTap)
        => MacOSInputEventPolicy.CreateHidEventMask(useSessionSystemDefinedTap);

    internal static ulong CreateSystemDefinedEventMask()
        => MacOSInputEventPolicy.CreateSystemDefinedEventMask();

    internal static CoreGraphics.CGEventTapOptions CreateObserveOnlyTapOptions()
        => MacOSInputEventPolicy.CreateObserveOnlyTapOptions();

    private static bool IsMouseEvent(CoreGraphics.CGEventType type)
    {
        return type is not (CoreGraphics.CGEventType.KeyDown or CoreGraphics.CGEventType.KeyUp or CoreGraphics.CGEventType.FlagsChanged or CoreGraphics.CGEventType.SystemDefined);
    }

    private static bool IsKeyEvent(CoreGraphics.CGEventType type)
    {
        return type is CoreGraphics.CGEventType.KeyDown or CoreGraphics.CGEventType.KeyUp or CoreGraphics.CGEventType.FlagsChanged or CoreGraphics.CGEventType.SystemDefined;
    }

    internal static bool ShouldIgnoreKeyboardEvent(long eventSourceUserData)
        => MacOSInputEventPolicy.ShouldIgnoreKeyboardEvent(eventSourceUserData);

    internal static bool ShouldReenableEventTap(CoreGraphics.CGEventType type)
        => MacOSInputEventPolicy.ShouldReenableEventTap(type);

    internal static long GetCurrentTimestamp()
        => MacOSInputEventPolicy.GetCurrentTimestamp();

    internal static long GetMonotonicTimestampMicroseconds() =>
        ToMicroseconds(Stopwatch.GetTimestamp(), Stopwatch.Frequency);

    internal static long ResolveEventTimestampMicroseconds(
        ulong eventTimestampNanoseconds,
        long fallbackTimestampMicroseconds)
    {
        if (eventTimestampNanoseconds is 0)
        {
            return fallbackTimestampMicroseconds;
        }

        return checked((long)(eventTimestampNanoseconds / 1_000UL));
    }

    internal static long ToMicroseconds(long timestamp, long frequency)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frequency, 0);
        return checked(
            (timestamp / frequency * 1_000_000L)
            + (timestamp % frequency * 1_000_000L / frequency));
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        StopCapture();
        GC.SuppressFinalize(this);
    }

    private void QueueInput(CapturedInputEventArgs inputEvent)
    {
        MacOSInputEventDispatcher? dispatcher = Volatile.Read(ref _inputDispatcher);
        if (dispatcher?.TryEnqueue(inputEvent) is true || _stopRequested)
        {
            return;
        }

        if (Interlocked.Exchange(ref _dispatchOverflowSignaled, 1) is not 0)
        {
            return;
        }

        _stopRequested = true;
        RequestStop();
        QueueCaptureError(
            $"The macOS input event queue exceeded {MacOSInputEventDispatcher.DefaultCapacity} events; capture was stopped to avoid recording incomplete input.");
    }

    private void DispatchInputEvent(CapturedInputEventArgs inputEvent) =>
        InputReceived?.Invoke(this, inputEvent);

    private void ReportDispatchError(Exception exception) =>
        RaiseCaptureError($"Error processing captured input: {exception.Message}");

    private void QueueCaptureError(string message) =>
        ThreadPool.UnsafeQueueUserWorkItem(
            static state => state.Capture.RaiseCaptureError(state.Message),
            (Capture: this, Message: message),
            preferLocal: false);

    private void RaiseCaptureError(string message)
    {
        try
        {
            CaptureError?.Invoke(this, new InputCaptureErrorEventArgs(message));
        }
        catch (Exception errorHandlerException) when (errorHandlerException is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MacOSInputCapture] Error handler threw: {errorHandlerException}");
        }
    }
}
