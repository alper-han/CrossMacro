
namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Base class for X11 Input Capture implementations.
/// Handles X server connection, XInput2 initialization, and the main event loop.
/// </summary>
public abstract class X11CaptureBase : IInputCapture
{
    private protected IntPtr _display;
    private protected IntPtr _rootWindow;
    private Thread? _captureThread;
    private protected volatile bool _isRunning;
    private bool _disposed;
    private CancellationTokenRegistration _startCancellationRegistration;
    private Task? _startupTask;
    private TaskCompletionSource<object?>? _startupCompletionSource;

    private protected bool _captureMouse;
    private protected bool _captureKeyboard;

    public abstract string ProviderName { get; }

    public bool IsSupported
    {
        get
        {
            try
            {
                var dpy = X11Native.XOpenDisplay(display: null);
                if (dpy == IntPtr.Zero)
                {
                    return false;
                }

                int major = XInput2Consts.XINPUT2_MAJOR_VERSION;
                int minor = XInput2Consts.XINPUT2_MINOR_VERSION;
                int res = X11Native.XIQueryVersion(dpy, ref major, ref minor);
                _ = X11Native.XCloseDisplay(dpy);

                return res is 0;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return false;
            }
        }
    }

    public event EventHandler<CapturedInputEventArgs>? InputReceived;
    public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

    public void Configure(bool captureMouse, bool captureKeyboard)
    {
        _captureMouse = captureMouse;
        _captureKeyboard = captureKeyboard;
    }

    public Task StartAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isRunning)
        {
            return _startupTask ?? Task.CompletedTask;
        }

        ct.ThrowIfCancellationRequested();

        _isRunning = true;
        var startupCompletionSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _startupCompletionSource = startupCompletionSource;
        _startupTask = startupCompletionSource.Task;

        return StartAsyncCoreAsync(startupCompletionSource, ct);
    }

    public void StopCapture()
    {
        _isRunning = false;
        var startCancellationRegistration = _startCancellationRegistration;
        _startCancellationRegistration = default;
        startCancellationRegistration.Dispose();
        _ = _startupCompletionSource?.TrySetCanceled(startCancellationRegistration.Token);

        var captureThread = _captureThread;
        if (captureThread is not null && captureThread.IsAlive && !ReferenceEquals(Thread.CurrentThread, captureThread))
        {
            _ = captureThread.Join(500);
        }
    }

    private void HandleStartCancellation(TaskCompletionSource<object?> startupCompletionSource, CancellationToken cancellationToken)
    {
        _isRunning = false;
        _ = startupCompletionSource.TrySetCanceled(cancellationToken);
    }

    private async ValueTask DisposeStartCancellationRegistrationAsync()
    {
        var startCancellationRegistration = _startCancellationRegistration;
        _startCancellationRegistration = default;
        await startCancellationRegistration.DisposeAsync().ConfigureAwait(false);
    }

    private async Task StartAsyncCoreAsync(
        TaskCompletionSource<object?> startupCompletionSource,
        CancellationToken ct)
    {
        await DisposeStartCancellationRegistrationAsync().ConfigureAwait(false);
        _startCancellationRegistration = ct.Register(() => HandleStartCancellation(startupCompletionSource, ct));

        _captureThread = new Thread(() => CaptureLoop(startupCompletionSource))
        {
            IsBackground = true,
            Name = GetType().Name,
        };
        _captureThread.Start();

        _ = await startupCompletionSource.Task.ConfigureAwait(false);
    }

    private void CaptureLoop(TaskCompletionSource<object?> startupCompletionSource)
    {
        try
        {
            _display = X11Native.XOpenDisplay(display: null);
            if (_display == IntPtr.Zero)
            {
                FailStartup(startupCompletionSource, "Failed to open X Display");
                return;
            }

            if (!InitializeXInput(startupCompletionSource))
            {
                return;
            }

            OnCaptureStarted();
            _ = startupCompletionSource.TrySetResult(null);

            RunEventLoop();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            FailStartup(startupCompletionSource, ex);
        }
        finally
        {
            CloseDisplayOnce();

            if (ReferenceEquals(_captureThread, Thread.CurrentThread))
            {
                _captureThread = null;
            }
        }
    }

    private bool InitializeXInput(TaskCompletionSource<object?> startupCompletionSource)
    {
        _rootWindow = X11Native.XDefaultRootWindow(_display);
        int major = XInput2Consts.XINPUT2_MAJOR_VERSION;
        int minor = XInput2Consts.XINPUT2_MINOR_VERSION;
        if (X11Native.XIQueryVersion(_display, ref major, ref minor) is not 0)
        {
            FailStartup(startupCompletionSource, "XInput2 extension not available");
            return false;
        }

        var maskBytes = CreateEventMask(_captureMouse, _captureKeyboard);

        IntPtr maskPtr = Marshal.AllocHGlobal(maskBytes.Length);
        try
        {
            Marshal.Copy(maskBytes, 0, maskPtr, maskBytes.Length);
            var mask = new XIEventMask
            {
                DeviceId = XInput2Consts.XIAllMasterDevices,
                MaskLen = maskBytes.Length,
                Mask = maskPtr,
            };
            _ = X11Native.XISelectEvents(_display, _rootWindow, ref mask, 1);
            _ = X11Native.XFlush(_display);
        }
        finally
        {
            Marshal.FreeHGlobal(maskPtr);
        }

        return true;
    }

    internal static byte[] CreateEventMask(bool captureMouse, bool captureKeyboard)
    {
        var maskBytes = new byte[4];
        if (captureKeyboard)
        {
            XInput2Consts.SetMask(maskBytes, XInput2Consts.XI_RawKeyPress);
            XInput2Consts.SetMask(maskBytes, XInput2Consts.XI_RawKeyRelease);
        }

        if (captureMouse)
        {
            XInput2Consts.SetMask(maskBytes, XInput2Consts.XI_RawButtonPress);
            XInput2Consts.SetMask(maskBytes, XInput2Consts.XI_RawButtonRelease);
            XInput2Consts.SetMask(maskBytes, XInput2Consts.XI_RawMotion);
        }

        return maskBytes;
    }

    private void RunEventLoop()
    {
        IntPtr eventPtr = Marshal.AllocHGlobal(XInput2Consts.XEVENT_STRUCT_SIZE);
        try
        {
            while (_isRunning)
            {
                if (X11Native.XPending(_display) > 0)
                {
                    _ = X11Native.XNextEvent(_display, eventPtr);
                    ProcessPendingEvent(eventPtr);
                }
                else
                {
                    OnLoopIdle();
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(eventPtr);
        }
    }

    private void ProcessPendingEvent(IntPtr eventPtr)
    {
        var xEvent = Marshal.PtrToStructure<XEvent>(eventPtr);
        if (xEvent.xcookie.type != XInput2Consts.GenericEvent || !X11Native.XGetEventData(_display, eventPtr))
        {
            return;
        }

        try
        {
            ProcessGenericEvent(Marshal.PtrToStructure<XGenericEventCookie>(eventPtr));
        }
        finally
        {
            X11Native.XFreeEventData(_display, eventPtr);
        }
    }

    private void FailStartup(TaskCompletionSource<object?> startupCompletionSource, string message)
    {
        FailStartup(startupCompletionSource, new InvalidOperationException(message));
    }

    private void FailStartup(TaskCompletionSource<object?> startupCompletionSource, Exception exception)
    {
        if (!startupCompletionSource.TrySetException(exception) &&
            !startupCompletionSource.Task.IsCanceled)
        {
            CaptureError?.Invoke(this, new InputCaptureErrorEventArgs(exception.Message));
        }
    }

    /// <summary>
    /// Called after X11 connection and selection are established, before the loop starts.
    /// </summary>
    protected virtual void OnCaptureStarted() { /* Empty */ }

    /// <summary>
    /// Called when no X events are pending. Waits on the X connection instead
    /// of continuously waking the capture thread.
    /// </summary>
    protected virtual void OnLoopIdle()
    {
        _ = LinuxFileDescriptorNative.PollReadable(
            X11Native.XConnectionNumber(_display),
            timeoutMilliseconds: 100);
    }

    /// <summary>
    /// Called before processing a Key/Button event. subclasses can override to flush pending motion.
    /// </summary>
    protected virtual void FlushPendingMotion() { /* Empty */ }

    /// <summary>
    /// Handles motion events.
    /// </summary>
    protected abstract void ProcessMotion(XGenericEventCookie cookie);

    private void ProcessGenericEvent(XGenericEventCookie cookie)
    {
        if (cookie.evtype == XInput2Consts.XI_RawMotion)
        {
            ProcessMotion(cookie);
            return;
        }

        var rawEvent = Marshal.PtrToStructure<XIRawEvent>(cookie.data);

        if (cookie.evtype is XInput2Consts.XI_RawKeyPress or XInput2Consts.XI_RawKeyRelease)
        {
            ProcessKeyboardEvent(cookie, rawEvent);
            return;
        }

        if (cookie.evtype is XInput2Consts.XI_RawButtonPress or XInput2Consts.XI_RawButtonRelease)
        {
            ProcessMouseButtonEvent(cookie, rawEvent);
        }
    }

    private void ProcessKeyboardEvent(XGenericEventCookie cookie, XIRawEvent rawEvent)
    {
        FlushPendingMotion();

        int code = rawEvent.detail - LinuxConstants.X11ToLinuxKeycodeOffset;
        int value = cookie.evtype == XInput2Consts.XI_RawKeyPress ? 1 : 0;
        var args = new CapturedInputEvent
        {
            Type = InputEventType.Key,
            Code = (ushort)code,
            Value = value,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            DeviceName = ProviderName,
        };
        InputReceived?.Invoke(this, new CapturedInputEventArgs(args));
    }

    private void ProcessMouseButtonEvent(XGenericEventCookie cookie, XIRawEvent rawEvent)
    {
        FlushPendingMotion();

        int code = rawEvent.detail;
        int value = cookie.evtype == XInput2Consts.XI_RawButtonPress ? 1 : 0;
        InputEventType type = InputEventType.MouseButton;

        if (code is >= XInput2Consts.X11_SCROLL_UP and <= XInput2Consts.X11_SCROLL_RIGHT)
        {
            if (value is 0)
            {
                return;
            }

            type = InputEventType.MouseScroll;
            (code, value) = MapScroll(code);
        }
        else
        {
            code = MapX11ButtonToLinux(code);
        }

        var args = new CapturedInputEvent
        {
            Type = type,
            Code = (ushort)code,
            Value = value,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            DeviceName = ProviderName,
        };
        InputReceived?.Invoke(this, new CapturedInputEventArgs(args));
    }

    private static (int Code, int Value) MapScroll(int code)
    {
        if (code is XInput2Consts.X11_SCROLL_UP or XInput2Consts.X11_SCROLL_DOWN)
        {
            return (XInput2Consts.SCROLL_AXIS_VERTICAL,
                code == XInput2Consts.X11_SCROLL_UP ? XInput2Consts.SCROLL_DELTA : -XInput2Consts.SCROLL_DELTA);
        }

        return (XInput2Consts.SCROLL_AXIS_HORIZONTAL,
            code == XInput2Consts.X11_SCROLL_RIGHT ? XInput2Consts.SCROLL_DELTA : -XInput2Consts.SCROLL_DELTA);
    }

    private static int MapX11ButtonToLinux(int x11Btn)
    {
        // Mapping based on linux/input-event-codes.h
        return x11Btn switch
        {
            1 => UInputNative.BTN_LEFT,
            2 => UInputNative.BTN_MIDDLE,
            3 => UInputNative.BTN_RIGHT,
            8 => UInputNative.BTN_SIDE,
            9 => UInputNative.BTN_EXTRA,
            _ => x11Btn, // Unknown
        };
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        StopCapture();

        // Close only if the capture thread has exited; otherwise its finally owns the close
        // (the thread may still be using the display after the 500 ms Join window).
        var captureThread = _captureThread;
        if (captureThread is null || !captureThread.IsAlive)
        {
            CloseDisplayOnce();
        }

        _disposed = true;
    }

    private void CloseDisplayOnce()
    {
        var display = Interlocked.Exchange(ref _display, IntPtr.Zero);
        if (display != IntPtr.Zero)
        {
            _ = X11Native.XCloseDisplay(display);
        }
    }

    // Helper for subclasses to emit events
    protected void OnInputReceived(CapturedInputEvent args)
    {
        InputReceived?.Invoke(this, new CapturedInputEventArgs(args));
    }
}
