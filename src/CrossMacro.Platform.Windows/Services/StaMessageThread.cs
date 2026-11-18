
namespace CrossMacro.Platform.Windows.Services;

[SupportedOSPlatform("windows")]
internal sealed class StaMessageThread : IDisposable
{
    private readonly Thread _thread;
    private readonly ConcurrentQueue<Action<Exception?>> _workQueue = new();
    private uint _threadId;
    private readonly AutoResetEvent _readyEvent = new(initialState: false);
    private Exception? _startupException;
    private int _isClosing;

    public IntPtr MessageWindowHandle { get; private set; }

    public StaMessageThread(string name)
    {
        _thread = new Thread(Run)
        {
            Name = name,
            IsBackground = true,
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        _ = _readyEvent.WaitOne();
        if (_startupException is not null)
        {
            _readyEvent.Dispose();
            throw new InvalidOperationException("Failed to initialize the Windows STA message thread.", _startupException);
        }
    }

    private void Run()
    {
        var oleInitialized = false;
        var startupSignaled = false;
        try
        {
            int hr = Ole32.OleInitialize(IntPtr.Zero);
            if (hr is < 0 and not Ole32.RpcEChangedMode)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
            oleInitialized = hr >= 0;

            _threadId = Kernel32.GetCurrentThreadId();
            if (!TryInitializeWindow(out string className, out var hInstance))
            {
                ReportStartupFailure();
                startupSignaled = true;
                return;
            }

            try
            {
                _ = User32.PeekMessage(out _, IntPtr.Zero, 0, 0, User32.PM_NOREMOVE);
                _ = _readyEvent.Set();
                startupSignaled = true;
                RunMessageLoop();
            }
            finally
            {
                DestroyMessageWindow(className, hInstance);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (!startupSignaled)
            {
                ReportStartupFailure(ex);
            }
        }
        finally
        {
            FailPendingWork();
            if (oleInitialized)
            {
                Ole32.OleUninitialize();
            }
        }
    }

    // Static root: the native thunk is only valid while the delegate instance is alive.
    private static readonly User32.WindowProc s_windowProc = User32.DefWindowProc;

    private bool TryInitializeWindow(out string className, out IntPtr hInstance)
    {
        className = "CrossMacro_MessageOnlyWindow_" + Guid.NewGuid().ToString("N");
        hInstance = Kernel32.GetModuleHandle(lpModuleName: null);
        var classNamePointer = Marshal.StringToCoTaskMemUni(className);
        var windowProcPointer = Marshal.GetFunctionPointerForDelegate(s_windowProc);

        var wndClass = new WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            lpfnWndProc = windowProcPointer,
            lpszClassName = classNamePointer,
            hInstance = hInstance,
        };

        try
        {
            if (User32.RegisterClassEx(ref wndClass) is 0)
            {
                return false;
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(classNamePointer);
        }

        MessageWindowHandle = User32.CreateWindowEx(
            0,
            className,
            "CrossMacro_Clipboard_Host",
            0,
            0, 0, 0, 0,
            User32.HwndMessage,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        if (MessageWindowHandle == IntPtr.Zero)
        {
            _ = User32.UnregisterClass(className, hInstance);
            return false;
        }

        return true;
    }

    private void DestroyMessageWindow(string className, IntPtr hInstance)
    {
        if (MessageWindowHandle != IntPtr.Zero)
        {
            _ = User32.DestroyWindow(MessageWindowHandle);
        }
        _ = User32.UnregisterClass(className, hInstance);
    }

    private void RunMessageLoop()
    {
        int bRet;
        while ((bRet = User32.GetMessage(out var msg, IntPtr.Zero, 0, 0)) is not 0)
        {
            if (bRet == -1)
            {
                break;
            }

            if (msg.message == User32.WM_APP)
            {
                ProcessWorkQueue();
            }
            else
            {
                _ = User32.TranslateMessage(ref msg);
                _ = User32.DispatchMessage(ref msg);
            }
        }
    }

    private void ProcessWorkQueue()
    {
        while (_workQueue.TryDequeue(out var action))
        {
            action(null);
        }
    }

    public Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        if (Volatile.Read(ref _isClosing) is not 0)
        {
            return Task.FromException<T>(new ObjectDisposedException(nameof(StaMessageThread)));
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration? cancellationRegistration = null;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationRegistration = cancellationToken.Register(
                static state =>
                {
                    ArgumentNullException.ThrowIfNull(state);
                    var (completion, token) = ((TaskCompletionSource<T>, CancellationToken))state;
                    _ = completion.TrySetCanceled(token);
                },
                (tcs, cancellationToken));

            _ = tcs.Task.ContinueWith(
                _ => cancellationRegistration?.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        _workQueue.Enqueue(failure =>
        {
            if (tcs.Task.IsCompleted)
            {
                return;
            }

            if (failure is not null)
            {
                _ = tcs.TrySetException(failure);
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _ = tcs.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = tcs.TrySetResult(action());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _ = tcs.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _ = tcs.TrySetException(ex);
            }
        });

        if (Volatile.Read(ref _isClosing) is not 0 || !User32.PostThreadMessage(_threadId, User32.WM_APP, IntPtr.Zero, IntPtr.Zero))
        {
            _ = tcs.TrySetException(new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to queue work on the Windows STA message thread."));
        }

        return tcs.Task;
    }

    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return InvokeAsync<bool>(() =>
        {
            action();
            return true;
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isClosing, 1) is not 0)
        {
            return;
        }

        _ = User32.PostThreadMessage(_threadId, User32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _ = _thread.Join(1000);
        FailPendingWork();
        _readyEvent.Dispose();
    }

    private void ReportStartupFailure(Exception? exception = null)
    {
        _startupException = exception ?? new Win32Exception(Marshal.GetLastPInvokeError());
        _ = _readyEvent.Set();
    }

    private void FailPendingWork()
    {
        var failure = new ObjectDisposedException(nameof(StaMessageThread));
        while (_workQueue.TryDequeue(out var action))
        {
            action(failure);
        }
    }
}
