using System.Runtime.InteropServices;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandExtImageCopyOutputCapture : IDisposable
{
    private const int SessionRoundtripLimit = 5;
    private const int FrameDispatchLimit = 40;
    private readonly WaylandLibrary _library;
    private readonly WaylandProtocolTables _protocol;
    private readonly IntPtr _display;
    private readonly WaylandRegistryState _registry;
    private readonly IntPtr _output;
    private readonly IntPtr _source;
    private IntPtr _buffer;
    private WaylandExtImageCopySessionState? _bufferSessionState;
    private WaylandShmBuffer? _shm;
    private bool _disposed;

    public WaylandExtImageCopyOutputCapture(
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

        try
        {
            _source = _library.CreateExtImageSource(_registry.ExtOutputSourceManager, _output, _protocol.ExtCaptureSource);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public ExtImageCopyFrame Capture(ScreenRect logicalBounds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var session = _library.CreateExtImageSession(_registry.ExtCopyManager, _source, _protocol.ExtCopySession);
        try
        {
            var sessionState = WaitForSession(session);
            EnsureBuffer(sessionState);
            var shm = _shm ?? throw new ObjectDisposedException(nameof(WaylandExtImageCopyOutputCapture));
            var frame = _library.CreateExtImageFrame(session, _protocol.ExtCopyFrame);
            try
            {
                var frameState = new WaylandExtImageCopyFrameState();
                _library.AddDispatcher(frame, frameState.DispatcherPtr);
                _library.AttachExtImageFrameBuffer(frame, _buffer);
                _library.DamageExtImageFrameBuffer(frame, 0, 0, checked((int)sessionState.Width), checked((int)sessionState.Height));
                _library.CaptureExtImageFrame(frame);
                WaitForReady(frameState);
                return CreateFrame(logicalBounds, sessionState, shm);
            }
            finally
            {
                _library.DestroyExtImageFrame(frame);
            }
        }
        finally
        {
            _library.DestroyExtImageSession(session);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DestroyBuffer();

        if (_source != IntPtr.Zero)
        {
            _library.DestroyExtImageSource(_source);
        }

    }

    private WaylandExtImageCopySessionState WaitForSession(IntPtr session)
    {
        var sessionState = new WaylandExtImageCopySessionState();
        _library.AddDispatcher(session, sessionState.DispatcherPtr);
        for (var i = 0; i < SessionRoundtripLimit && !sessionState.Done && !sessionState.Stopped; i++)
        {
            _library.DisplayRoundtrip(_display);
        }

        if (!sessionState.Done || sessionState.Stopped || sessionState.Width == 0 || sessionState.Height == 0 || !sessionState.HasSupportedShmFormat)
        {
            throw new InvalidOperationException(
                $"Unsupported ext-image-copy session constraints. format=0x{sessionState.ShmFormat:x8} formats={sessionState.FormatAdvertisedShmFormats()} size={sessionState.Width}x{sessionState.Height} stopped={sessionState.Stopped}.");
        }

        return sessionState;
    }

    private void WaitForReady(WaylandExtImageCopyFrameState frameState)
    {
        for (var i = 0; i < FrameDispatchLimit && !frameState.Ready && !frameState.Failed; i++)
        {
            _library.DisplayDispatch(_display);
        }

        if (!frameState.Ready)
        {
            throw new InvalidOperationException($"ext-image-copy frame failed or timed out. reason={frameState.FailureReason}");
        }
    }

    private void EnsureBuffer(WaylandExtImageCopySessionState sessionState)
    {
        if (_buffer != IntPtr.Zero && _bufferSessionState is { } current && HasSameBufferConstraints(current, sessionState))
        {
            return;
        }

        var shm = CreateShm(sessionState);
        var pool = _library.CreateShmPool(_registry.Shm, shm.Fd, shm.Size, _protocol.WlShmPool);
        IntPtr buffer = IntPtr.Zero;
        try
        {
            buffer = _library.CreateBuffer(
                pool,
                checked((int)sessionState.Width),
                checked((int)sessionState.Height),
                sessionState.Stride,
                sessionState.ShmFormat,
                _protocol.WlBuffer);
        }
        catch
        {
            if (buffer != IntPtr.Zero)
            {
                _library.DestroyBuffer(buffer);
            }

            shm.Dispose();
            throw;
        }
        finally
        {
            _library.DestroyShmPool(pool);
        }

        DestroyBuffer();
        _buffer = buffer;
        _shm = shm;
        _bufferSessionState = sessionState;
    }

    private void DestroyBuffer()
    {
        if (_buffer != IntPtr.Zero)
        {
            _library.DestroyBuffer(_buffer);
            _buffer = IntPtr.Zero;
        }

        _shm?.Dispose();
        _shm = null;
        _bufferSessionState = null;
    }

    private static bool HasSameBufferConstraints(WaylandExtImageCopySessionState current, WaylandExtImageCopySessionState next) =>
        current.Width == next.Width &&
        current.Height == next.Height &&
        current.Stride == next.Stride &&
        current.ShmFormat == next.ShmFormat;

    private static WaylandShmBuffer CreateShm(WaylandExtImageCopySessionState sessionState)
    {
        var size = checked(sessionState.Stride * (int)sessionState.Height);
        return WaylandShmBuffer.Create(size);
    }

    private static ExtImageCopyFrame CreateFrame(ScreenRect logicalBounds, WaylandExtImageCopySessionState sessionState, WaylandShmBuffer shm)
    {
        var byteCount = checked(sessionState.Stride * (int)sessionState.Height);
        var pixels = new byte[byteCount];
        Marshal.Copy(shm.Address, pixels, 0, byteCount);
        if (!WaylandExtImageCopyShmFormats.TryMap(sessionState.ShmFormat, out var format))
        {
            throw new InvalidOperationException($"ext-image-copy selected unsupported SHM format 0x{sessionState.ShmFormat:x8}.");
        }

        return new ExtImageCopyFrame(logicalBounds, sessionState.Stride, format, pixels);
    }

    private sealed class WaylandExtImageCopySessionState
    {
        private readonly SessionDispatcher _dispatcher;
        private readonly List<uint> _advertisedShmFormats = [];

        public WaylandExtImageCopySessionState()
        {
            _dispatcher = Dispatch;
            DispatcherPtr = Marshal.GetFunctionPointerForDelegate(_dispatcher);
        }

        private delegate int SessionDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

        public IntPtr DispatcherPtr { get; }
        public bool Done { get; private set; }
        public bool Stopped { get; private set; }
        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public uint ShmFormat { get; private set; }
        public bool HasSupportedShmFormat { get; private set; }
        public int Stride => checked((int)Width * ScreenFrame.GetBytesPerPixel(ScreenPixelFormat.Xrgb8888));

        public string FormatAdvertisedShmFormats() => WaylandExtImageCopyShmFormats.FormatAdvertisedFormats(CollectionsMarshal.AsSpan(_advertisedShmFormats));

        private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
        {
            var size = Marshal.SizeOf<WlArgument>();
            switch (opcode)
            {
                case 0:
                    Width = Marshal.PtrToStructure<WlArgument>(args).u;
                    Height = Marshal.PtrToStructure<WlArgument>(args + size).u;
                    break;
                case 1:
                    var format = Marshal.PtrToStructure<WlArgument>(args).u;
                    _advertisedShmFormats.Add(format);
                    if (WaylandExtImageCopyShmFormats.TryMap(format, out _) &&
                        (!HasSupportedShmFormat || WaylandExtImageCopyShmFormats.ShouldReplaceSelectedFormat(format, ShmFormat)))
                    {
                        ShmFormat = format;
                        HasSupportedShmFormat = true;
                    }

                    break;
                case 4:
                    Done = true;
                    break;
                case 5:
                    Stopped = true;
                    break;
            }

            return 0;
        }
    }

    private sealed class WaylandExtImageCopyFrameState
    {
        private readonly FrameDispatcher _dispatcher;

        public WaylandExtImageCopyFrameState()
        {
            _dispatcher = Dispatch;
            DispatcherPtr = Marshal.GetFunctionPointerForDelegate(_dispatcher);
        }

        private delegate int FrameDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

        public IntPtr DispatcherPtr { get; }
        public bool Ready { get; private set; }
        public bool Failed { get; private set; }
        public uint FailureReason { get; private set; }

        private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
        {
            switch (opcode)
            {
                case 3:
                    Ready = true;
                    break;
                case 4:
                    Failed = true;
                    FailureReason = Marshal.PtrToStructure<WlArgument>(args).u;
                    break;
            }

            return 0;
        }
    }
}
