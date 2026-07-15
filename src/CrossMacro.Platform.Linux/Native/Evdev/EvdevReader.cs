
namespace CrossMacro.Platform.Linux.Native.Evdev;

public class EvdevReader : IDisposable
{
    private readonly string _devicePath;
    private readonly object _lifecycleLock = new();
    private ReaderSession? _session;
    private bool _disposed;
    private bool _syncing;
    private byte[]? _lastKeyState;

    public string DeviceName { get; }

    public event Action<EvdevReader, UInputNative.input_event>? EventReceived;
    public event Action<Exception>? ErrorOccurred;

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

    public EvdevReader(string devicePath, string deviceName)
    {
        _devicePath = devicePath;
        DeviceName = deviceName;
    }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_session is not null) return;

            int fd = EvdevNative.open(_devicePath, EvdevNative.O_RDONLY | EvdevNative.O_NONBLOCK);
            if (fd < 0)
            {
                Log.Error("[EvdevReader] Failed to open device {Path} - Check permissions (need input group)", _devicePath);
                throw new InvalidOperationException($"Failed to open device {_devicePath}. Check permissions (need input group).");
            }

            var session = new ReaderSession(fd, new CancellationTokenSource());
            _syncing = false;
            _lastKeyState = null;
            _session = session;
            session.ReadTask = Task.Run(() => ReadLoop(session));

            Log.Debug("[EvdevReader] Started reading from {Device} ({Path})", DeviceName, _devicePath);
        }
    }

    public void Stop()
    {
        ReaderSession? session;
        lock (_lifecycleLock)
        {
            session = _session;
            if (session is null) return;
            session.IsStopping = true;
            session.Cancellation.Cancel();
        }

        Task? readTask = session.ReadTask;
        if (readTask is not null && Task.CurrentId != readTask.Id)
        {
            try
            {
                readTask.Wait(200);
            }
            catch (AggregateException)
            {
            }
        }

        Log.Debug("[EvdevReader] Stopped reading from {Device}", DeviceName);
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
                    break;

                if (bytesRead.ToInt64() == eventSize)
                {
                    var ev = Marshal.PtrToStructure<UInputNative.input_event>(buffer);

                    if (ev.type == UInputNative.EV_SYN && ev.code == UInputNative.SYN_DROPPED)
                    {
                        _syncing = true;
                        Log.Warning("[{Device}] SYN_DROPPED: Events lost, waiting for SYN_REPORT to resync", DeviceName);
                        continue;
                    }

                    if (ev.type == UInputNative.EV_SYN && ev.code == UInputNative.SYN_REPORT)
                    {
                        if (_syncing)
                        {
                            ResyncKeyState(session.Fd, token);
                            _syncing = false;
                        }
                        if (!token.IsCancellationRequested)
                            EventReceived?.Invoke(this, ev);
                        continue;
                    }

                    if (_syncing)
                        continue;

                    if (!token.IsCancellationRequested)
                        EventReceived?.Invoke(this, ev);
                }
                else if (bytesRead.ToInt64() < 0)
                {
                    var errno = Marshal.GetLastWin32Error();

                    if (errno is 9)
                    {
                        break;
                    }

                    if (errno is 4)
                    {
                        continue;
                    }

                    if (errno is 11)
                    {
                        token.WaitHandle.WaitOne(10);
                        continue;
                    }

                    throw new System.IO.IOException($"Read error: {errno}");
                }
                else if (bytesRead.ToInt64() == 0)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                ErrorOccurred?.Invoke(ex);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            EvdevNative.close(session.Fd);
            lock (_lifecycleLock)
            {
                if (ReferenceEquals(_session, session))
                {
                    _session = null;
                    session.Cancellation.Dispose();
                }
            }
        }
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
                continue;

            bool currentlyPressed = (currentKeyState[byteIndex] & (1 << bitIndex)) is not 0;
            bool wasPressed = (_lastKeyState[byteIndex] & (1 << bitIndex)) is not 0;

            if (token.IsCancellationRequested)
                return;

            if (currentlyPressed != wasPressed)
            {
                var ev = new UInputNative.input_event
                {
                    type = UInputNative.EV_KEY,
                    code = (ushort)keyCode,
                    value = currentlyPressed ? 1 : 0,
                };
                EventReceived?.Invoke(this, ev);
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
                continue;

            bool pressed = (keyState[byteIndex] & (1 << bitIndex)) is not 0;

            if (token.IsCancellationRequested)
                return;

            if (pressed)
            {
                var ev = new UInputNative.input_event
                {
                    type = UInputNative.EV_KEY,
                    code = (ushort)keyCode,
                    value = 1,
                };
                EventReceived?.Invoke(this, ev);
            }
        }
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        Stop();
    }

    private sealed class ReaderSession
    {
        public ReaderSession(int fd, CancellationTokenSource cancellation)
        {
            Fd = fd;
            Cancellation = cancellation;
        }

        public int Fd { get; }
        public CancellationTokenSource Cancellation { get; }
        public Task? ReadTask { get; set; }
        public bool IsStopping { get; set; }
    }
}
