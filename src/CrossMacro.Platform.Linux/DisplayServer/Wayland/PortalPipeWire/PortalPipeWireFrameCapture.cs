using CrossMacro.Platform.Abstractions;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal sealed partial class PortalPipeWireFrameCapture : IPortalPipeWireFrameCapture
{
    private readonly PipeWireLibrary _lib;
    private readonly uint _nodeId;
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
    private readonly IntPtr _bufferParameter;
    private readonly IntPtr _connectParameters;
    private bool _threadLoopStarted;
    private bool _disposed;
    private PortalPipeWireFrame? _frame;
    private string? _error;
    private int _lastState;
    private uint _lastParamId;
    private int _processCallbacks;
    private int _allocatedBuffers;
    private int _nullBuffers;
    private int _missingDataArrays;
    private int _missingDataPointers;
    private uint _lastDataCount;
    private uint _lastDataType;
    private uint _lastDataFlags;
    private uint _lastMaxSize;
    private uint _lastChunkOffset;
    private uint _lastChunkSize;
    private int _lastChunkStride;

    public PortalPipeWireFrameCapture(SafeFileHandle pipeWireRemote, uint nodeId, int width, int height)
    {
        try
        {
            _nodeId = nodeId;
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
                _stream = CreateStream(_lib, _core);
                (_listener, _events) = AddListener();
            }
            finally
            {
                _lib.ThreadLoopUnlock(_threadLoop);
            }

            _formatParameter = SpaFormatPodBuilder.CreateRawVideoEnumFormat(width, height);
            _bufferParameter = SpaFormatPodBuilder.CreateCpuBufferParams(width, height);
            _connectParameters = Marshal.AllocHGlobal(IntPtr.Size * 2);
            Marshal.WriteIntPtr(_connectParameters, _formatParameter);
            Marshal.WriteIntPtr(_connectParameters + IntPtr.Size, _bufferParameter);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public Task<PortalPipeWireFrameResult> CaptureFrameAsync(ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (options.CancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.Canceled, "XDG Desktop Portal PipeWire capture was canceled before it started."));
        }

        try
        {
            return Task.FromResult(CaptureOne(options));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.Canceled, "XDG Desktop Portal PipeWire capture was canceled."));
        }
        catch (TimeoutException ex)
        {
            return Task.FromResult(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureTimeout, ex.Message));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException)
        {
            return Task.FromResult(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_threadLoopStarted && _threadLoop != IntPtr.Zero)
        {
            _lib.ThreadLoopStop(_threadLoop);
        }

        if (_stream != IntPtr.Zero) _lib.StreamDestroy(_stream);
        if (_core != IntPtr.Zero) _lib.CoreDisconnect(_core);
        if (_context != IntPtr.Zero) _lib.ContextDestroy(_context);
        if (_threadLoop != IntPtr.Zero) _lib.ThreadLoopDestroy(_threadLoop);
        Free(_listener);
        Free(_events);
        Free(_connectParameters);
        Free(_formatParameter);
        Free(_bufferParameter);
        if (_selfHandle.IsAllocated) _selfHandle.Free();
        _lib?.Dispose();
    }

    private PortalPipeWireFrameResult CaptureOne(ScreenReadOptions options)
    {
        var timeout = options.Timeout ?? TimeSpan.FromMinutes(2);
        _lib.ThreadLoopLock(_threadLoop);
        try
        {
            var rc = _lib.StreamConnect(
                _stream,
                PipeWireDirection.Input,
                _nodeId,
                PipeWireStreamFlags.Autoconnect | PipeWireStreamFlags.MapBuffers | PipeWireStreamFlags.AllocBuffers,
                _connectParameters,
                2);
            if (rc < 0)
            {
                return PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, $"pw_stream_connect failed rc={rc}.");
            }

            var deadline = DateTimeOffset.UtcNow + timeout;
            while (_frame is null && _error is null && DateTimeOffset.UtcNow < deadline)
            {
                options.CancellationToken.ThrowIfCancellationRequested();
                _lib.ThreadLoopTimedWait(_threadLoop, 1);
            }
        }
        finally
        {
            _lib.ThreadLoopUnlock(_threadLoop);
        }

        if (_error is not null)
        {
            return PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, _error);
        }

        return _frame is not null
            ? PortalPipeWireFrameResult.Success(_frame)
            : throw new TimeoutException(BuildTimeoutMessage());
    }

    private static IntPtr CreateThreadLoop(PipeWireLibrary lib)
    {
        var loop = lib.ThreadLoopNew("crossmacro-portal-pw", IntPtr.Zero);
        if (loop == IntPtr.Zero) throw new InvalidOperationException("pw_thread_loop_new failed.");
        var rc = lib.ThreadLoopStart(loop);
        if (rc < 0)
        {
            lib.ThreadLoopDestroy(loop);
            throw new InvalidOperationException($"pw_thread_loop_start failed rc={rc}.");
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
        var fd = PortalPipeWireLibc.dup((int)remote.DangerousGetHandle());
        if (fd < 0) throw new InvalidOperationException($"dup(pipewire fd) failed errno={Marshal.GetLastPInvokeError()}.");
        var core = lib.ContextConnectFd(context, fd, IntPtr.Zero, UIntPtr.Zero);
        if (core != IntPtr.Zero)
        {
            return core;
        }

        PortalPipeWireLibc.close(fd);
        throw new InvalidOperationException("pw_context_connect_fd failed.");
    }

    private static IntPtr CreateStream(PipeWireLibrary lib, IntPtr core)
    {
        var props = lib.PropertiesNew("media.type", "Video");
        lib.PropertiesSet(props, "media.category", "Capture");
        lib.PropertiesSet(props, "media.role", "Screen");
        var stream = lib.StreamNew(core, "CrossMacro Portal Capture", props);
        return stream == IntPtr.Zero ? throw new InvalidOperationException("pw_stream_new failed.") : stream;
    }

    private static void Free(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
    }

    private string BuildTimeoutMessage() =>
        $"Timed out waiting for a PipeWire frame. state={GetStateName(_lastState)} lastParamId={_lastParamId} " +
        $"processCallbacks={_processCallbacks} allocatedBuffers={_allocatedBuffers} nullBuffers={_nullBuffers} missingDataArrays={_missingDataArrays} " +
        $"missingDataPointers={_missingDataPointers} lastDataCount={_lastDataCount} lastDataType={_lastDataType} " +
        $"lastDataFlags=0x{_lastDataFlags:X} lastMaxSize={_lastMaxSize} lastChunkOffset={_lastChunkOffset} " +
        $"lastChunkSize={_lastChunkSize} lastChunkStride={_lastChunkStride}";

    private static string GetStateName(int state) => state switch
    {
        -1 => "error",
        0 => "unconnected",
        1 => "connecting",
        2 => "paused",
        3 => "streaming",
        _ => state.ToString()
    };
}
