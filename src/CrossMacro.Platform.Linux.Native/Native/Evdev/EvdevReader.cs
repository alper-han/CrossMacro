
namespace CrossMacro.Platform.Linux.Native.Evdev;

public sealed class EvdevReader : IDisposable, IAsyncDisposable
{
    private const int BadFileDescriptor = 9;
    private const int InterruptedSystemCall = 4;
    private const int ResourceTemporarilyUnavailable = 11;
    private const int BatchEvents = 64;

    private static readonly int EventSize = Unsafe.SizeOf<UInputNative.input_event>();

    private readonly string _devicePath;
    private readonly Lock _lifecycleLock = new();
    private readonly Func<string, int> _open;
    private readonly Action<int> _close;
    private readonly Func<CancellationToken, Task>? _readLoopOverride;
    private ReaderSession? _session;
    private bool _disposed;

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

            var session = CreateSession(fd);
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

    }

    internal static bool IsSynchronizationLost(UInputNative.input_event inputEvent) =>
        inputEvent.type is UInputNative.EV_SYN && inputEvent.code is UInputNative.SYN_DROPPED;

    private ReaderSession CreateSession(int fd)
    {
        var wakeFd = -1;
        if (_readLoopOverride is null)
        {
            wakeFd = EvdevNative.eventfd(0, EvdevNative.EFD_CLOEXEC | EvdevNative.EFD_NONBLOCK);
            if (wakeFd < 0)
            {
                var errno = Marshal.GetLastPInvokeError();
                _close(fd);
                throw new IOException($"eventfd failed: {errno.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        var session = new ReaderSession(fd, wakeFd, new CancellationTokenSource());
        if (wakeFd >= 0)
        {
            session.WakeRegistration = session.Cancellation.Token.Register(static state => ((ReaderSession)state!).Wake(), session);
        }

        return session;
    }

    private void ReadLoop(ReaderSession session)
    {
        var buffer = new byte[EventSize * BatchEvents];
        CancellationToken token = session.Cancellation.Token;
        var synchronizer = new EvdevEventSynchronizer(
            DeviceName,
            keys => EvdevNative.ioctl(session.Fd, EvdevNative.EVIOCGKEY, keys) >= 0,
            inputEvent => RaiseEventReceived(inputEvent, token));

        try
        {
            synchronizer.SeedKeyState();
            while (!token.IsCancellationRequested)
            {
                nint bytesRead = EvdevNative.read(session.Fd, buffer, buffer.Length);
                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (bytesRead > 0)
                {
                    Dispatch(buffer.AsSpan(0, (int)bytesRead), synchronizer, token);
                    continue;
                }

                if (bytesRead is 0 || !HandleReadFailure(session))
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
            CleanupSession(session);
        }
    }

    private static void Dispatch(ReadOnlySpan<byte> bytes, EvdevEventSynchronizer synchronizer, CancellationToken token)
    {
        for (var offset = 0; offset + EventSize <= bytes.Length && !token.IsCancellationRequested; offset += EventSize)
        {
            var inputEvent = MemoryMarshal.Read<UInputNative.input_event>(bytes[offset..]);
            synchronizer.Process(inputEvent);
        }
    }

    private void RaiseEventReceived(UInputNative.input_event inputEvent, CancellationToken token)
    {
        if (!token.IsCancellationRequested)
        {
            EventReceived?.Invoke(this, new EvdevInputEventArgs(inputEvent));
        }
    }

    /// <returns><see langword="true"/> when reading should continue.</returns>
    private static bool HandleReadFailure(ReaderSession session)
    {
        var errno = Marshal.GetLastPInvokeError();
        return errno switch
        {
            BadFileDescriptor => false,
            InterruptedSystemCall => true,
            ResourceTemporarilyUnavailable => WaitForData(session),
            _ => throw new IOException($"Read error: {errno.ToString(CultureInfo.InvariantCulture)}"),
        };
    }

    /// <returns><see langword="false"/> when the session was cancelled while waiting.</returns>
    private static bool WaitForData(ReaderSession session)
    {
        Span<EvdevNative.PollFd> fds =
        [
            new EvdevNative.PollFd { Fd = session.Fd, Events = EvdevNative.POLLIN },
            new EvdevNative.PollFd { Fd = session.WakeFd, Events = EvdevNative.POLLIN },
        ];

        while (EvdevNative.poll(fds, (nuint)fds.Length, timeout: -1) < 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            if (errno is not InterruptedSystemCall)
            {
                throw new IOException($"Poll error: {errno.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        return fds[1].Revents is 0;
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
            CleanupSession(session);
        }
    }

    private void CleanupSession(ReaderSession session)
    {
        session.WakeRegistration.Dispose();
        if (session.WakeFd >= 0)
        {
            _close(session.WakeFd);
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
    }

    private sealed class ReaderSession(int fd, int wakeFd, CancellationTokenSource cancellation)
    {
        public int Fd { get; } = fd;
        public int WakeFd { get; } = wakeFd;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public CancellationTokenRegistration WakeRegistration { get; set; }
        public Task? ReadTask { get; set; }
        public bool IsStopping { get; set; }

        public void Wake()
        {
            ulong signal = 1;
            _ = EvdevNative.write(WakeFd, ref signal, sizeof(ulong));
        }
    }
}
