
namespace CrossMacro.Platform.Windows.Services;

[SupportedOSPlatform("windows")]
internal sealed partial class StaMessageThread : IDisposable
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
            int hr = OleInitialize(IntPtr.Zero);
            if (hr is < 0 and not unchecked((int)0x80010106))
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
                _ = PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
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
                OleUninitialize();
            }
        }
    }

    // Static root: the native thunk is only valid while the delegate instance is alive.
    private static readonly WndProcDelegate s_windowProc = DefWindowProc;

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
            if (RegisterClassEx(ref wndClass) is 0)
            {
                return false;
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(classNamePointer);
        }

        var hwndMessage = new IntPtr(-3);
        MessageWindowHandle = CreateWindowEx(
            0,
            className,
            "CrossMacro_Clipboard_Host",
            0,
            0, 0, 0, 0,
            hwndMessage,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        if (MessageWindowHandle == IntPtr.Zero)
        {
            _ = UnregisterClass(className, hInstance);
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
        _ = UnregisterClass(className, hInstance);
    }

    private void RunMessageLoop()
    {
        int bRet;
        while ((bRet = GetMessage(out var msg, IntPtr.Zero, 0, 0)) is not 0)
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

    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        if (Volatile.Read(ref _isClosing) is not 0)
        {
            return Task.FromException<T>(new ObjectDisposedException(nameof(StaMessageThread)));
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

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

            try
            {
                _ = tcs.TrySetResult(action());
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _ = tcs.TrySetException(ex);
            }
        });

        if (Volatile.Read(ref _isClosing) is not 0 || !User32.PostThreadMessage(_threadId, User32.WM_APP, IntPtr.Zero, IntPtr.Zero))
        {
            _ = tcs.TrySetException(new Win32Exception(Marshal.GetLastWin32Error(), "Failed to queue work on the Windows STA message thread."));
        }

        return tcs.Task;
    }

    public Task InvokeAsync(Action action)
    {
        return InvokeAsync<bool>(() =>
        {
            action();
            return true;
        });
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
        _startupException = exception ?? new Win32Exception(Marshal.GetLastWin32Error());
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

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "DefWindowProcW")]
    private static partial IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "GetMessageW")]
    private static partial int GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "RegisterClassExW")]
    private static partial ushort RegisterClassEx(ref WndClassEx lpwcx);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "CreateWindowExW")]
    private static partial IntPtr CreateWindowEx(
        uint dwExStyle,
        [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
        [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
        uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [LibraryImport("user32.dll", SetLastError = true, EntryPoint = "UnregisterClassW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterClass([MarshalAs(UnmanagedType.LPWStr)] string lpClassName, IntPtr hInstance);

    [LibraryImport("ole32.dll")]
    private static partial int OleInitialize(IntPtr pvReserved);

    [LibraryImport("ole32.dll")]
    private static partial void OleUninitialize();

    [StructLayout(LayoutKind.Sequential)]
    private struct WndClassEx
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        public IntPtr lpszClassName;
        public IntPtr hIconSm;
    }
}
