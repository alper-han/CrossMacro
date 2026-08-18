namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal sealed partial class PortalPipeWireFrameCapture : IPortalPipeWireFrameCapture
{
    private readonly PipeWireLibrary _lib;
    private readonly PortalPipeWireConnection _connection;
    private PortalPipeWireConnectionLease? _connectionLease;
    private readonly uint _nodeId;
    private readonly ulong? _pipeWireSerial;
    private readonly int _width;
    private readonly int _height;
    private readonly PipeWireLibrary.StreamStateChanged _stateChanged;
    private readonly PipeWireLibrary.StreamParamChanged _paramChanged;
    private readonly PipeWireLibrary.StreamBufferChanged _addBuffer;
    private readonly PipeWireLibrary.StreamBufferChanged _removeBuffer;
    private readonly PipeWireLibrary.StreamProcess _process;
    private readonly Action<string> _connectionError;
    private readonly GCHandle _selfHandle;
    private readonly IntPtr _threadLoop;
    private readonly IntPtr _core;
    private IntPtr _stream;
    private IntPtr _listener;
    private IntPtr _events;
    private readonly IntPtr _formatParameter;
    private readonly IntPtr _connectParameters;
    private readonly Lock _pendingGate = new();
    private readonly Lock _streamGate = new();
    private readonly PipeWireFrameSequence _frameSequence = new();
    private readonly PortalPipeWireFrameCache _frameCache;
    private bool _disposed;
    private bool _connected;
    private PendingCapture? _pending;
    private string? _error;
    private PipeWireVideoLayout? _negotiatedLayout;

    public PortalPipeWireFrameCapture(SafeFileHandle pipeWireRemote, uint nodeId, int width, int height, ulong? pipeWireSerial = null)
    {
        ArgumentNullException.ThrowIfNull(pipeWireRemote);
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "PipeWire stream width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "PipeWire stream height must be positive.");
        }

        try
        {
            _nodeId = nodeId;
            _pipeWireSerial = pipeWireSerial;
            _width = width;
            _height = height;
            _frameCache = new PortalPipeWireFrameCache(width, height);
            _connectionLease = PortalPipeWireConnection.Acquire(pipeWireRemote);
            _connection = _connectionLease.Connection;
            _lib = _connection.Library;
            _threadLoop = _connection.ThreadLoop;
            _core = _connection.Core;
            _stateChanged = OnStateChanged;
            _paramChanged = OnParamChanged;
            _addBuffer = OnAddBuffer;
            _removeBuffer = OnRemoveBuffer;
            _process = OnProcess;
            _connectionError = OnConnectionError;
            _selfHandle = GCHandle.Alloc(this);
            _connection.Error += _connectionError;
            _connection.WithLock(() =>
            {
                _stream = CreateStream(_lib, _core, _pipeWireSerial);
                (_listener, _events) = AddListener();
            });

            _formatParameter = SpaFormatPodBuilder.CreateRawVideoEnumFormat(width, height);
            _connectParameters = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(_connectParameters, _formatParameter);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public Task<PortalPipeWireFrameResult> CaptureFrameAsync(ScreenReadOptions options) =>
        CaptureFrameAsync(new ScreenRect(0, 0, _width, _height), options);

    public async Task<PortalPipeWireFrameResult> CaptureFrameAsync(ScreenRect region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed), this);
        ValidateRegion(region);
        if (options.CancellationToken.IsCancellationRequested)
        {
            return PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.Canceled, "XDG Desktop Portal PipeWire capture was canceled before it started.");
        }

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
        var timing = PipeWireCaptureTiming.Create(options.Timeout ?? ScreenReadOptions.DefaultTimeout);
        if (timing.Timeout != Timeout.InfiniteTimeSpan)
        {
            timeoutCancellation.CancelAfter(timing.Timeout);
        }

        PendingCapture pending;
        lock (_pendingGate)
        {
            if (_disposed)
            {
                return PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.Canceled, "XDG Desktop Portal PipeWire capture was disposed.");
            }

            if (_pending is not null)
            {
                return PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, "A PipeWire frame request is already pending for this stream.");
            }

            pending = new PendingCapture(region, _frameSequence.Snapshot(), options.CancellationToken);
            _pending = pending;
        }

        if (_frameCache.TryCreateFrame(region, out var cachedFrame))
        {
            CompletePending(PortalPipeWireFrameResult.Success(cachedFrame!), pending);
            return await pending.Completion.Task.ConfigureAwait(false);
        }

        using var timeoutRegistration = timeoutCancellation.Token.Register(static state =>
        {
            var cancellation = (PendingCaptureCancellation)state!;
            cancellation.Capture.CancelPending(cancellation.Pending);
        }, new PendingCaptureCancellation(this, pending));

        try
        {
            if (!pending.Completion.Task.IsCompleted)
            {
                ConnectIfNeeded();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException)
        {
            CompletePending(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message));
        }

        return await pending.Completion.Task.ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (_streamGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _frameCache.Clear();
            CompletePending(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.Canceled, "XDG Desktop Portal PipeWire capture was disposed."));
            if (_stream != IntPtr.Zero)
            {
                _connection.WithLock(() => _lib.StreamDestroy(_stream));
                _stream = IntPtr.Zero;
            }

            if (_connectionLease is not null)
            {
                _connection.Error -= _connectionError;
            }
            Free(_listener);
            Free(_events);
            Free(_connectParameters);
            Free(_formatParameter);
            if (_selfHandle.IsAllocated)
            {
                _selfHandle.Free();
            }

            _connectionLease?.Dispose();
            _connectionLease = null;
        }
    }

    private void ConnectIfNeeded()
    {
        lock (_streamGate)
        {
            if (_disposed)
            {
                return;
            }

            if (_error is not null)
            {
                CompletePending(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, _error));
                return;
            }

            if (_connected)
            {
                return;
            }

            _connection.WithLock(() =>
            {
                var result = _lib.StreamConnect(
                    _stream,
                    PipeWireDirection.Input,
                    _pipeWireSerial is not null ? PipeWireConstants.PwIdAny : _nodeId,
                    PipeWireStreamOption.Autoconnect | PipeWireStreamOption.MapBuffers,
                    _connectParameters,
                    1);
                if (result < 0)
                {
                    CompletePending(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, $"pw_stream_connect failed rc={result.ToString(CultureInfo.InvariantCulture)}."));
                    return;
                }

                _connected = true;
            });
        }
    }

    private void OnConnectionError(string message)
    {
        if (Volatile.Read(ref _disposed))
        {
            return;
        }

        _error = message;
        _frameCache.Clear();
        CompletePending(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, message));
        _lib.ThreadLoopSignal(_threadLoop, waitForAccept: false);
    }

    private void CancelPending(PendingCapture expected)
    {
        var errorKind = expected.UserCancellationToken.IsCancellationRequested
            ? ScreenReadErrorKind.Canceled
            : ScreenReadErrorKind.CaptureTimeout;
        CompletePending(
            PortalPipeWireFrameResult.Failure(errorKind, errorKind is ScreenReadErrorKind.Canceled
                ? "XDG Desktop Portal PipeWire capture was canceled."
                : "Timed out waiting for a PipeWire frame."),
            expected);
    }

    private void CompletePending(PortalPipeWireFrameResult result, PendingCapture? expected = null)
    {
        PendingCapture? pending;
        lock (_pendingGate)
        {
            pending = _pending;
            if (pending is null || (expected is not null && !ReferenceEquals(pending, expected)))
            {
                return;
            }

            _pending = null;
        }

        _ = pending.Completion.TrySetResult(result);
        if (_threadLoop != IntPtr.Zero)
        {
            _lib.ThreadLoopSignal(_threadLoop, waitForAccept: false);
        }
    }

    private void ValidateRegion(ScreenRect region)
    {
        if (region.X < 0 || region.Y < 0 || region.Right > _width || region.Bottom > _height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(region),
                region,
                $"PipeWire capture region must be inside the stream bounds 0,0 {_width.ToString(CultureInfo.InvariantCulture)}x{_height.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private sealed class PendingCapture(ScreenRect region, long startGeneration, CancellationToken userCancellationToken)
    {
        public ScreenRect Region { get; } = region;
        public CancellationToken UserCancellationToken { get; } = userCancellationToken;
        public long StartGeneration { get; } = startGeneration;
        public TaskCompletionSource<PortalPipeWireFrameResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class PendingCaptureCancellation(PortalPipeWireFrameCapture capture, PendingCapture pending)
    {
        public PortalPipeWireFrameCapture Capture { get; } = capture;
        public PendingCapture Pending { get; } = pending;
    }

    private static IntPtr CreateStream(PipeWireLibrary library, IntPtr core, ulong? pipeWireSerial)
    {
        var properties = library.PropertiesNew("media.type", "Video");
        _ = library.PropertiesSet(properties, "media.category", "Capture");
        _ = library.PropertiesSet(properties, "media.role", "Screen");
        if (pipeWireSerial is { } serial)
        {
            _ = library.PropertiesSet(properties, "target.object", serial.ToString(CultureInfo.InvariantCulture));
        }

        var stream = library.StreamNew(core, "CrossMacro Portal Capture", properties);
        return stream == IntPtr.Zero ? throw new InvalidOperationException("pw_stream_new failed.") : stream;
    }

    private static void Free(IntPtr pointer)
    {
        if (pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}
