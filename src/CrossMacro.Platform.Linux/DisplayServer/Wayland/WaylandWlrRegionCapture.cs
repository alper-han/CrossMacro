
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandWlrRegionCapture(
    WaylandLibrary library,
    WaylandProtocolTables protocol,
    IntPtr display,
    WaylandRegistryState registry,
    IntPtr output) : IDisposable
{
    private const int ConstraintRoundtripLimit = 5;
    private const int FrameDispatchLimit = 40;
    private WaylandLibrary Library { get; } = library;
    private WaylandProtocolTables Protocol { get; } = protocol;
    private readonly IntPtr _display = display;
    private readonly WaylandRegistryState _registry = registry;
    private readonly IntPtr _output = output;
    private bool _disposed;

    public WlrScreencopyFrame Capture(ScreenRect outputRegion, ScreenRect logicalBounds, WaylandCaptureCancellation cancellation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellation.ThrowIfCancellationRequested();
        var frameState = new WaylandWlrFrameState();
        var frame = IntPtr.Zero;
        try
        {
            frame = Library.WlrCaptureOutputRegion(_registry.WlrScreencopyManager, _output, outputRegion, Protocol.WlrScreencopyFrame);
            WaitForConstraints(frame, frameState, cancellation);
            cancellation.ThrowIfCancellationRequested();
            using var shm = CreateShm(frameState, cancellation);
            cancellation.ThrowIfCancellationRequested();
            var pool = Library.CreateShmPool(_registry.Shm, shm.Fd, shm.Size, Protocol.WlShmPool);
            if (pool == IntPtr.Zero)
            {
                throw new InvalidOperationException("wl_shm.create_pool returned NULL.");
            }
            var buffer = IntPtr.Zero;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                buffer = Library.CreateBuffer(pool, checked((int)frameState.Width), checked((int)frameState.Height), checked((int)frameState.Stride), frameState.Format, Protocol.WlBuffer);
                if (buffer == IntPtr.Zero)
                {
                    throw new InvalidOperationException("wl_shm_pool.create_buffer returned NULL.");
                }
                var bufferState = new WaylandBufferState();
                try
                {
                    _ = Library.AddDispatcher(buffer, bufferState.DispatcherPtr);
                    bufferState.MarkSubmitted();
                    cancellation.ThrowIfCancellationRequested();
                    Library.WlrFrameCopy(frame, buffer);
                    WaitForReady(frameState, cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    return CreateFrame(logicalBounds, frameState, shm);
                }
                finally
                {
                    if (buffer != IntPtr.Zero)
                    {
                        Library.DestroyBuffer(buffer);
                    }

                    bufferState.Dispose();
                }
            }
            finally
            {
                Library.DestroyShmPool(pool);
            }
        }
        finally
        {
            if (frame != IntPtr.Zero)
            {
                Library.DestroyWlrFrame(frame);
            }

            frameState.Dispose();
        }
    }

    public void Dispose() => _disposed = true;

    private void WaitForConstraints(IntPtr frame, WaylandWlrFrameState frameState, WaylandCaptureCancellation cancellation)
    {
        _ = Library.AddDispatcher(frame, frameState.DispatcherPtr);
        for (var i = 0; i < ConstraintRoundtripLimit && !frameState.CanCreateBuffer && !frameState.Failed; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            Library.DisplayRoundtrip(_display, cancellation);
        }

        if (!frameState.CanCreateBuffer || frameState.Failed)
        {
            throw new InvalidOperationException("wlr-screencopy did not provide SHM buffer constraints.");
        }

        if (!WaylandShmFormats.TryMap(frameState.Format, out _))
        {
            throw new InvalidOperationException($"wlr-screencopy returned unsupported SHM format 0x{frameState.Format:x8}.");
        }

        if (!WaylandShmFormats.TryGetStride(frameState.Format, frameState.Width, out var minimumStride) ||
            frameState.Stride < (uint)minimumStride)
        {
            throw new InvalidOperationException(
                $"wlr-screencopy returned an invalid SHM stride. format=0x{frameState.Format:x8} " +
                $"width={frameState.Width.ToString(CultureInfo.InvariantCulture)} " +
                $"stride={frameState.Stride.ToString(CultureInfo.InvariantCulture)} " +
                $"minimumStride={minimumStride.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private void WaitForReady(WaylandWlrFrameState frameState, WaylandCaptureCancellation cancellation)
    {
        for (var i = 0; i < FrameDispatchLimit && !frameState.Ready && !frameState.Failed; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            Library.DisplayDispatch(_display, cancellation);
        }

        if (!frameState.Ready)
        {
            throw new InvalidOperationException("wlr-screencopy frame failed or timed out.");
        }
    }

    private static WaylandShmBuffer CreateShm(WaylandWlrFrameState frameState, WaylandCaptureCancellation cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var size = checked((int)frameState.Stride * (int)frameState.Height);
        return WaylandShmBuffer.Create(size);
    }

    private static WlrScreencopyFrame CreateFrame(ScreenRect logicalBounds, WaylandWlrFrameState frameState, WaylandShmBuffer shm)
    {
        var stride = checked((int)frameState.Stride);
        var byteCount = checked(stride * (int)frameState.Height);
        var pixels = new byte[byteCount];
        System.Runtime.InteropServices.Marshal.Copy(shm.Address, pixels, 0, byteCount);
        if (!WaylandShmFormats.TryMap(frameState.Format, out var format))
        {
            throw new InvalidOperationException($"wlr-screencopy returned unsupported SHM format 0x{frameState.Format:x8}.");
        }
        return new WlrScreencopyFrame(
            logicalBounds,
            stride,
            format,
            pixels,
            physicalWidth: checked((int)frameState.Width),
            physicalHeight: checked((int)frameState.Height));
    }
}
