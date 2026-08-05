
namespace CrossMacro.Platform.Linux.Native.Evdev;

public sealed class EvdevReader : IDisposable, IAsyncDisposable
{
    private readonly string _devicePath;
    private readonly Lock _lifecycleLock = new();
    private readonly Func<string, int> _open;
    private readonly Action<int> _close;
    private readonly Func<CancellationToken, Task>? _readLoopOverride;
    private ReaderSession? _session;
    private bool _disposed;
    private bool _syncing;
    private byte[]? _lastKeyState;

    public EvdevReader(string devicePath, string deviceName)
        : this(
            devicePath,
            deviceName,
            static path => EvdevNative.open(path, EvdevNative.O_RDONLY | EvdevNative.O_NONBLOCK),
            static fd => _ = EvdevNative.close(fd),
            readLoopOverride: null)
    {
    }

    internal EvdevReader(
        string devicePath,
        string deviceName,
        Func<string, int> open,
        Action<int> close,
        Func<CancellationToken, Task>? readLoopOverride)
    {
        _devicePath = devicePath;
        DeviceName = deviceName;
        _open = open;
        _close = close;
        _readLoopOverride = readLoopOverride;
    }

    public string DeviceName { get; }

    public event EventHandler<EvdevInputEventArgs>? EventReceived;
    public event EventHandler<EvdevErrorEventArgs>? ErrorOccurred;

    public bool IsListening
    {
        get
        {
            lock (_lifecycleLock)
            {
                return _session is { IsStopping: false };
            }
        }
    }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_session is not null)
            {
                return;
            }

            int fd = _open(_devicePath);
            if (fd < 0)
            {
                Log.LogError("[EvdevReader] Failed to open device {Path} - Check permissions (need input group)", _devicePath);
                throw new InvalidOperationException($"Failed to open device {_devicePath}. Check permissions (need input group).");
            }

            var session = new ReaderSession(fd, new CancellationTokenSource());
            _syncing = false;
            _lastKeyState = null;
            _session = session;
            session.ReadTask = _readLoopOverride is null
                ? Task.Run(() => ReadLoop(session), CancellationToken.None)
                : Task.Run(() => RunReadLoopOverrideAsync(session, _readLoopOverride), CancellationToken.None);

            Log.Debug("[EvdevReader] Started reading from {Device} ({Path})", DeviceName, _devicePath);
        }
    }

    public void Stop()
    {
        ReaderSession? session;
        lock (_lifecycleLock)
        {
            session = _session;
            if (session is null)
            {
                return;
            }

            session.IsStopping = true;
            session.Cancellation.Cancel();
        }

        Task? readTask = session.ReadTask;
        if (readTask is not null && Task.CurrentId != readTask.Id)
        {
            try
            {
                _ = readTask.Wait(TimeSpan.FromMilliseconds(200), CancellationToken.None);
            }
            catch (AggregateException)
            {
                // expected when the read loop throws during shutdown; we are tearing down anyway.
            }
            catch (OperationCanceledException)
            {
                // expected after the reader cancellation is signaled.
            }
        }

        Log.Debug("[EvdevReader] Stopped reading from {Device}", DeviceName);
    }

    public async ValueTask DisposeAsync()
    {
        ReaderSession? session;
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            session = _session;
            _ = session?.IsStopping = true;
        }

        if (session is not null)
        {
            await session.Cancellation.CancelAsync().ConfigureAwait(false);
        }

        if (session?.ReadTask is not null && Task.CurrentId != session.ReadTask.Id)
        {
            try
            {
                await session.ReadTask.WaitAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected after the reader stop request.
            }
            catch (TimeoutException)
            {
                // A slow read loop is allowed to finish after the bounded shutdown wait.
            }
        }

        GC.SuppressFinalize(this);
    }

    private void ReadLoop(ReaderSession session)
    {
        int eventSize = Marshal.SizeOf<UInputNative.input_event>();
        IntPtr buffer = Marshal.AllocHGlobal(eventSize);
        CancellationToken token = session.Cancellation.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                IntPtr bytesRead = EvdevNative.read(session.Fd, buffer, (IntPtr)eventSize);
                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (!ProcessReadResult(session, buffer, bytesRead, token))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected during stop — cancellation is the normal exit path.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (!token.IsCancellationRequested)
            {
                ErrorOccurred?.Invoke(this, new EvdevErrorEventArgs(ex));
            }
        }
        finally
        {
            CleanupSession(session, buffer);
        }
    }

    private bool ProcessReadResult(ReaderSession session, IntPtr buffer, IntPtr bytesRead, CancellationToken token)
    {
        long count = bytesRead.ToInt64();
        if (count == Marshal.SizeOf<UInputNative.input_event>())
        {
            ProcessInputEvent(session, buffer, token);
            return true;
        }

        if (count == 0)
        {
            return false;
        }

        if (count > 0)
        {
            return true;
        }

        var errno = Marshal.GetLastWin32Error();
        if (errno is 9)
        {
            return false;
        }

        if (errno is 4)
        {
            return true;
        }

        if (errno is 11)
        {
            _ = token.WaitHandle.WaitOne(10);
            return true;
        }

        throw new IOException($"Read error: {errno.ToString(CultureInfo.InvariantCulture)}");
    }

    private void ProcessInputEvent(ReaderSession session, IntPtr buffer, CancellationToken token)
    {
        var ev = Marshal.PtrToStructure<UInputNative.input_event>(buffer);
        if (ev.type == UInputNative.EV_SYN && ev.code == UInputNative.SYN_DROPPED)
        {
            _syncing = true;
            Log.Warning("[{Device}] SYN_DROPPED: Events lost, waiting for SYN_REPORT to resync", DeviceName);
            return;
        }

        if (ev.type == UInputNative.EV_SYN && ev.code == UInputNative.SYN_REPORT && _syncing)
        {
            ResyncKeyState(session.Fd, token);
            _syncing = false;
        }

        if (!_syncing && !token.IsCancellationRequested)
        {
            EventReceived?.Invoke(this, new EvdevInputEventArgs(ev));
        }
    }

    private async Task RunReadLoopOverrideAsync(ReaderSession session, Func<CancellationToken, Task> readLoopOverride)
    {
        try
        {
            await readLoopOverride(session.Cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (session.Cancellation.IsCancellationRequested)
        {
            // Cancellation is the normal exit path for an overridden read loop.
        }
        finally
        {
            CleanupSession(session, IntPtr.Zero);
        }
    }

    private void CleanupSession(ReaderSession session, IntPtr buffer)
    {
        if (buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(buffer);
        }
        _close(session.Fd);
        lock (_lifecycleLock)
        {
            if (ReferenceEquals(_session, session))
            {
                _session = null;
            }
        }

        session.Cancellation.Dispose();
    }

    private void ResyncKeyState(int fd, CancellationToken token)
    {
        byte[] currentKeyState = new byte[96];
        int result = EvdevNative.ioctl(fd, EvdevNative.EVIOCGKEY, currentKeyState);

        if (result < 0)
        {
            Log.Warning("[{Device}] Failed to read key state during resync (errno: {Errno})",
                DeviceName, Marshal.GetLastWin32Error());
            return;
        }

        if (_lastKeyState is null)
        {
            _lastKeyState = new byte[96];
            EmitCurrentKeyState(currentKeyState, token);
            Array.Copy(currentKeyState, _lastKeyState, 96);
            Log.Debug("[{Device}] Initial key state sync completed", DeviceName);
            return;
        }

        for (int keyCode = 0; keyCode < 768; keyCode++)
        {
            int byteIndex = keyCode / 8;
            int bitIndex = keyCode % 8;

            if (byteIndex >= currentKeyState.Length)
            {
                continue;
            }

            bool currentlyPressed = (currentKeyState[byteIndex] & (1 << bitIndex)) is not 0;
            bool wasPressed = (_lastKeyState[byteIndex] & (1 << bitIndex)) is not 0;

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (currentlyPressed != wasPressed)
            {
                var ev = new UInputNative.input_event
                {
                    type = UInputNative.EV_KEY,
                    code = (ushort)keyCode,
                    value = currentlyPressed ? 1 : 0,
                };
                EventReceived?.Invoke(this, new EvdevInputEventArgs(ev));
            }
        }

        Array.Copy(currentKeyState, _lastKeyState, 96);
        Log.Debug("[{Device}] Resync completed after SYN_DROPPED", DeviceName);
    }

    private void EmitCurrentKeyState(byte[] keyState, CancellationToken token)
    {
        for (int keyCode = 0; keyCode < 768; keyCode++)
        {
            int byteIndex = keyCode / 8;
            int bitIndex = keyCode % 8;

            if (byteIndex >= keyState.Length)
            {
                continue;
            }

            bool pressed = (keyState[byteIndex] & (1 << bitIndex)) is not 0;

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (pressed)
            {
                var ev = new UInputNative.input_event
                {
                    type = UInputNative.EV_KEY,
                    code = (ushort)keyCode,
                    value = 1,
                };
                EventReceived?.Invoke(this, new EvdevInputEventArgs(ev));
            }
        }
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Stop();
        GC.SuppressFinalize(this);
    }

    private sealed class ReaderSession(int fd, CancellationTokenSource cancellation)
    {
        public int Fd { get; } = fd;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public Task? ReadTask { get; set; }
        public bool IsStopping { get; set; }
    }
}
