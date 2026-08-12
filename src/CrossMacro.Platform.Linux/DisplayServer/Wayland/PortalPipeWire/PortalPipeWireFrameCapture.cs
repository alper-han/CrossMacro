
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal sealed partial class PortalPipeWireFrameCapture : IPortalPipeWireFrameCapture
{
    private readonly PipeWireLibrary _lib;
    private readonly uint _nodeId;
    private readonly ulong? _pipeWireSerial;
    private readonly int _width;
    private readonly int _height;
    private readonly PipeWireLibrary.StreamStateChanged _stateChanged;
    private readonly PipeWireLibrary.StreamParamChanged _paramChanged;
    private readonly PipeWireLibrary.StreamBufferChanged _addBuffer;
    private readonly PipeWireLibrary.StreamBufferChanged _removeBuffer;
    private readonly PipeWireLibrary.StreamProcess _process;
    private readonly GCHandle _selfHandle;
    private readonly IntPtr _threadLoop;
    private readonly IntPtr _context;
    private readonly IntPtr _core;
    private readonly IntPtr _stream;
    private readonly IntPtr _listener;
    private readonly IntPtr _events;
    private readonly IntPtr _formatParameter;
    private readonly IntPtr _connectParameters;
    private readonly bool _threadLoopStarted;
    private readonly Lock _pendingGate = new();
    private readonly Lock _streamGate = new();
    private readonly PipeWireFrameSequence _frameSequence = new();
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
            _lib = PipeWireLibrary.Load();
            _stateChanged = OnStateChanged;
            _paramChanged = OnParamChanged;
            _addBuffer = OnAddBuffer;
            _removeBuffer = OnRemoveBuffer;
            _process = OnProcess;
            _selfHandle = GCHandle.Alloc(this);
            _threadLoop = CreateThreadLoop(_lib);
            _threadLoopStarted = true;
            _lib.ThreadLoopLock(_threadLoop);
            try
            {
                _context = CreateContext(_lib, _threadLoop);
                _core = ConnectCore(_lib, _context, pipeWireRemote);
                _stream = CreateStream(_lib, _core, _pipeWireSerial);
                (_listener, _events) = AddListener();
            }
            finally
            {
                _lib.ThreadLoopUnlock(_threadLoop);
            }

            _formatParameter = SpaFormatPodBuilder.CreateRawVideoEnumFormat(width, height);
            _connectParameters = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(_connectParameters, _formatParameter);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
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
        var timing = PipeWireCaptureTiming.Create(
            _lib.SupportsStreamActivation,
            options.Timeout ?? TimeSpan.FromMinutes(2));
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

            pending = new PendingCapture(
                region,
                _frameSequence.Snapshot(),
                new PipeWireFrameAdmission(timing.RequiresSettlingFrame),
                options.CancellationToken);
            _pending = pending;
        }

        _ = timeoutCancellation.Token.Register(static state =>
        {
            var capture = (PortalPipeWireFrameCapture)state!;
            capture.CancelPending();
        }, this);

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

        var result = await pending.Completion.Task.ConfigureAwait(false);
        DeactivateIfIdle();
        return result;
    }

    public void Dispose()
    {
        lock (_streamGate)
        {
            if (_disposed)
            {
                return;
            }

            lock (_pendingGate)
            {
                _disposed = true;
            }

            CompletePending(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.Canceled, "XDG Desktop Portal PipeWire capture was disposed."));
            if (_threadLoopStarted && _threadLoop != IntPtr.Zero)
            {
                _lib.ThreadLoopStop(_threadLoop);
            }

            if (_stream != IntPtr.Zero)
            {
                _lib.StreamDestroy(_stream);
            }

            if (_core != IntPtr.Zero)
            {
                _ = _lib.CoreDisconnect(_core);
            }

            if (_context != IntPtr.Zero)
            {
                _lib.ContextDestroy(_context);
            }

            if (_threadLoop != IntPtr.Zero)
            {
                _lib.ThreadLoopDestroy(_threadLoop);
            }

            Free(_listener);
            Free(_events);
            Free(_connectParameters);
            Free(_formatParameter);
            if (_selfHandle.IsAllocated)
            {
                _selfHandle.Free();
            }

            _lib?.Dispose();
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
                _lib.ThreadLoopLock(_threadLoop);
                try
                {
                    ActivateIfNeeded();
                }
                finally
                {
                    _lib.ThreadLoopUnlock(_threadLoop);
                }

                return;
            }

            _lib.ThreadLoopLock(_threadLoop);
            try
            {
                var rc = _lib.StreamConnect(
                    _stream,
                    PipeWireDirection.Input,
                    _pipeWireSerial is { } ? PipeWireConstants.PwIdAny : _nodeId,
                    PipeWireStreamOption.Autoconnect
                        | (_lib.SupportsStreamActivation ? PipeWireStreamOption.Inactive : PipeWireStreamOption.None)
                        | PipeWireStreamOption.MapBuffers,
                    _connectParameters,
                    1);
                if (rc < 0)
                {
                    CompletePending(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, $"pw_stream_connect failed rc={rc.ToString(CultureInfo.InvariantCulture)}."));
                    return;
                }

                _connected = true;
                ActivateIfNeeded();
            }
            finally
            {
                _lib.ThreadLoopUnlock(_threadLoop);
            }
        }
    }

    private void ActivateIfNeeded()
    {
        if (_lib.SupportsStreamActivation && _lib.StreamSetActive(_stream, active: true) < 0)
        {
            CompletePending(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, "pw_stream_set_active(true) failed."));
        }
    }

    private void DeactivateIfIdle()
    {
        lock (_streamGate)
        {
            if (_disposed)
            {
                return;
            }

            lock (_pendingGate)
            {
                if (_pending is not null)
                {
                    return;
                }
            }

            if (_connected && _lib.SupportsStreamActivation)
            {
                _lib.ThreadLoopLock(_threadLoop);
                try
                {
                    _ = _lib.StreamSetActive(_stream, active: false);
                }
                finally
                {
                    _lib.ThreadLoopUnlock(_threadLoop);
                }
            }
        }
    }

    private void CancelPending()
    {
        var errorKind = _pending is { } pending && pending.UserCancellationToken.IsCancellationRequested
            ? ScreenReadErrorKind.Canceled
            : ScreenReadErrorKind.CaptureTimeout;
        CompletePending(PortalPipeWireFrameResult.Failure(errorKind, errorKind is ScreenReadErrorKind.Canceled
            ? "XDG Desktop Portal PipeWire capture was canceled."
            : "Timed out waiting for a PipeWire frame."));
    }

    private void CompletePending(PortalPipeWireFrameResult result)
    {
        PendingCapture? pending;
        lock (_pendingGate)
        {
            pending = _pending;
            _pending = null;
        }

        if (pending is not null)
        {
            _ = pending.Completion.TrySetResult(result);
            if (_threadLoop != IntPtr.Zero)
            {
                _lib.ThreadLoopSignal(_threadLoop, waitForAccept: false);
            }
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

    private sealed class PendingCapture(
        ScreenRect region,
        long startGeneration,
        PipeWireFrameAdmission frameAdmission,
        CancellationToken userCancellationToken)
    {
        public ScreenRect Region { get; } = region;
        public CancellationToken UserCancellationToken { get; } = userCancellationToken;
        public long StartGeneration { get; } = startGeneration;
        public PipeWireFrameAdmission FrameAdmission { get; } = frameAdmission;
        public TaskCompletionSource<PortalPipeWireFrameResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static IntPtr CreateThreadLoop(PipeWireLibrary lib)
    {
        var loop = lib.ThreadLoopNew("crossmacro-portal-pw", IntPtr.Zero);
        if (loop == IntPtr.Zero)
        {
            throw new InvalidOperationException("pw_thread_loop_new failed.");
        }

        var rc = lib.ThreadLoopStart(loop);
        if (rc < 0)
        {
            lib.ThreadLoopDestroy(loop);
            throw new InvalidOperationException($"pw_thread_loop_start failed rc={rc.ToString(CultureInfo.InvariantCulture)}.");
        }

        return loop;
    }

    private static IntPtr CreateContext(PipeWireLibrary lib, IntPtr loop)
    {
        var context = lib.ContextNew(lib.ThreadLoopGetLoop(loop), IntPtr.Zero, UIntPtr.Zero);
        return context == IntPtr.Zero ? throw new InvalidOperationException("pw_context_new failed.") : context;
    }

    private static IntPtr ConnectCore(PipeWireLibrary lib, IntPtr context, SafeFileHandle remote)
    {
        var fd = PortalPipeWireLibc.dup(remote);
        if (fd < 0)
        {
            throw new InvalidOperationException($"dup(pipewire fd) failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }

        var core = lib.ContextConnectFd(context, fd, IntPtr.Zero, UIntPtr.Zero);
        if (core != IntPtr.Zero)
        {
            return core;
        }

        _ = PortalPipeWireLibc.close(fd);
        throw new InvalidOperationException("pw_context_connect_fd failed.");
    }

    private static IntPtr CreateStream(PipeWireLibrary lib, IntPtr core, ulong? pipeWireSerial)
    {
        var props = lib.PropertiesNew("media.type", "Video");
        _ = lib.PropertiesSet(props, "media.category", "Capture");
        _ = lib.PropertiesSet(props, "media.role", "Screen");
        if (pipeWireSerial is { } serial)
        {
            _ = lib.PropertiesSet(props, "target.object", serial.ToString(CultureInfo.InvariantCulture));
        }

        var stream = lib.StreamNew(core, "CrossMacro Portal Capture", props);
        return stream == IntPtr.Zero ? throw new InvalidOperationException("pw_stream_new failed.") : stream;
    }

    private static void Free(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

}
