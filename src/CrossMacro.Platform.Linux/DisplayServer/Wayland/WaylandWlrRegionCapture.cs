using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandWlrRegionCapture : IDisposable
{
    private const int ConstraintRoundtripLimit = 5;
    private const int FrameDispatchLimit = 40;
    private const uint WlShmFormatArgb8888 = 0;
    private const uint WlShmFormatXrgb8888 = 1;
    private readonly WaylandLibrary _library;
    private readonly WaylandProtocolTables _protocol;
    private readonly IntPtr _display;
    private readonly WaylandRegistryState _registry;
    private readonly IntPtr _output;
    private bool _disposed;

    public WaylandWlrRegionCapture(
        WaylandLibrary library,
        WaylandProtocolTables protocol,
        IntPtr display,
        WaylandRegistryState registry,
        IntPtr output)
    {
        _library = library;
        _protocol = protocol;
        _display = display;
        _registry = registry;
        _output = output;
    }

    public WlrScreencopyFrame Capture(ScreenRect outputRegion, ScreenRect logicalBounds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var frame = _library.WlrCaptureOutputRegion(_registry.WlrScreencopyManager, _output, outputRegion, _protocol.WlrScreencopyFrame);
        try
        {
            var frameState = WaitForConstraints(frame);
            using var shm = CreateShm(frameState);
            var pool = _library.CreateShmPool(_registry.Shm, shm.Fd, shm.Size, _protocol.WlShmPool);
            var buffer = IntPtr.Zero;
            try
            {
                buffer = _library.CreateBuffer(pool, checked((int)frameState.Width), checked((int)frameState.Height), checked((int)frameState.Stride), frameState.Format, _protocol.WlBuffer);
                var bufferState = new WaylandBufferState();
                _library.AddDispatcher(buffer, bufferState.DispatcherPtr);
                bufferState.MarkSubmitted();
                _library.WlrFrameCopy(frame, buffer);
                WaitForReady(frameState);
                return CreateFrame(logicalBounds, frameState, shm);
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    _library.DestroyBuffer(buffer);
                }

                _library.DestroyShmPool(pool);
            }
        }
        finally
        {
            _library.DestroyWlrFrame(frame);
        }
    }

    public void Dispose() => _disposed = true;

    private WaylandWlrFrameState WaitForConstraints(IntPtr frame)
    {
        var frameState = new WaylandWlrFrameState();
        _library.AddDispatcher(frame, frameState.DispatcherPtr);
        for (var i = 0; i < ConstraintRoundtripLimit && !frameState.CanCreateBuffer && !frameState.Failed; i++)
        {
            _library.DisplayRoundtrip(_display);
        }

        if (!frameState.CanCreateBuffer || frameState.Failed)
        {
            throw new InvalidOperationException("wlr-screencopy did not provide SHM buffer constraints.");
        }

        if (frameState.Format is not (WlShmFormatXrgb8888 or WlShmFormatArgb8888))
        {
            throw new InvalidOperationException($"wlr-screencopy returned unsupported SHM format 0x{frameState.Format:x8}.");
        }

        return frameState;
    }

    private void WaitForReady(WaylandWlrFrameState frameState)
    {
        for (var i = 0; i < FrameDispatchLimit && !frameState.Ready && !frameState.Failed; i++)
        {
            _library.DisplayDispatch(_display);
        }

        if (!frameState.Ready)
        {
            throw new InvalidOperationException("wlr-screencopy frame failed or timed out.");
        }
    }

    private static WaylandShmBuffer CreateShm(WaylandWlrFrameState frameState)
    {
        var size = checked((int)frameState.Stride * (int)frameState.Height);
        return WaylandShmBuffer.Create(size);
    }

    private static WlrScreencopyFrame CreateFrame(ScreenRect logicalBounds, WaylandWlrFrameState frameState, WaylandShmBuffer shm)
    {
        var stride = checked((int)frameState.Stride);
        var byteCount = checked(stride * (int)frameState.Height);
        var pixels = new byte[byteCount];
        System.Runtime.InteropServices.Marshal.Copy(shm.Address, pixels, 0, byteCount);
        var format = frameState.Format == WlShmFormatXrgb8888 ? ScreenPixelFormat.Xrgb8888 : ScreenPixelFormat.Bgra8888;
        return new WlrScreencopyFrame(logicalBounds, stride, format, pixels);
    }
}
