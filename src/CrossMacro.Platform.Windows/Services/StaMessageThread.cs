
namespace CrossMacro.Platform.Windows.Services;

[SupportedOSPlatform("windows")]
internal sealed class StaMessageThread : IDisposable
{
    private readonly Thread _thread;
    private readonly ConcurrentQueue<Action<Exception?>> _workQueue = new();
    private IntPtr _hwnd;
    private uint _threadId;
    private readonly AutoResetEvent _readyEvent = new(initialState: false);
    private Exception? _startupException;
    private int _isClosing;

    public IntPtr MessageWindowHandle => _hwnd;

    public StaMessageThread(string name)
    {
        _thread = new Thread(Run)
        {
            Name = name,
            IsBackground = true,
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        _readyEvent.WaitOne();
        if (_startupException is not null)
        {
            _readyEvent.Dispose();
            throw new InvalidOperationException("Failed to initialize the Windows STA message thread.", _startupException);
        }
    }

    private void Run()
    {
        OleInitialize(IntPtr.Zero);
        try
        {
            _threadId = Kernel32.GetCurrentThreadId();

            string className = "CrossMacro_MessageOnlyWindow_" + Guid.NewGuid().ToString("N");
            var wndClass = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate<WndProcDelegate>(DefWindowProc),
                lpszClassName = className,
                hInstance = Kernel32.GetModuleHandle(lpModuleName: null),
            };

            if (RegisterClassEx(ref wndClass) is 0)
            {
                ReportStartupFailure();
                return;
            }

            var hwndMessage = new IntPtr(-3);

            _hwnd = CreateWindowEx(
                0,
                className,
                "CrossMacro_Clipboard_Host",
                0,
                0, 0, 0, 0,
                hwndMessage,
                IntPtr.Zero,
                wndClass.hInstance,
                IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                UnregisterClass(className, wndClass.hInstance);
                ReportStartupFailure();
                return;
            }

            PeekMessage(out _, IntPtr.Zero, 0, 0, 0);

            _readyEvent.Set();

            try
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
                        while (_workQueue.TryDequeue(out var action))
                        {
                            action(null);
                        }
                    }
                    else
                    {
                        User32.TranslateMessage(ref msg);
                        User32.DispatchMessage(ref msg);
                    }
                }
            }
            finally
            {
                if (_hwnd != IntPtr.Zero)
                {
                    User32.DestroyWindow(_hwnd);
                }
                UnregisterClass(className, wndClass.hInstance);
            }
        }
        finally
        {
            FailPendingWork();
            OleUninitialize();
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
                tcs.TrySetException(failure);
                return;
            }

            try
            {
                tcs.TrySetResult(action());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (Volatile.Read(ref _isClosing) is not 0 || !User32.PostThreadMessage(_threadId, User32.WM_APP, IntPtr.Zero, IntPtr.Zero))
        {
            tcs.TrySetException(new Win32Exception(Marshal.GetLastWin32Error(), "Failed to queue work on the Windows STA message thread."));
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

        User32.PostThreadMessage(_threadId, User32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
        FailPendingWork();
        _readyEvent.Dispose();
    }

    private void ReportStartupFailure()
    {
        _startupException = new Win32Exception(Marshal.GetLastWin32Error());
        _readyEvent.Set();
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

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "DefWindowProcW")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight, IntPtr hWndParent,
        IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    private static extern void OleUninitialize();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
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
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }
}
