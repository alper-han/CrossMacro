namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandExtCursorOutputSession : IDisposable
{
    private readonly WaylandLibrary _library;
    private readonly WaylandOutputInfo _output;
    private readonly Action<int, int> _positionChanged;
    private GCHandle _cursorDispatcherHandle;
    private GCHandle _captureDispatcherHandle;
    private IntPtr _source;
    private IntPtr _cursorSession;
    private IntPtr _captureSession;
    private bool _entered;
    private bool _hasPendingPosition;
    private int _pendingX;
    private int _pendingY;
    private int? _outputGeneration;
    private bool _disposed;

    public WaylandExtCursorOutputSession(
        WaylandLibrary library,
        WaylandProtocolTables protocol,
        WaylandRegistryState registry,
        WaylandOutputInfo output,
        IntPtr pointer,
        Action<int, int> positionChanged)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _positionChanged = positionChanged ?? throw new ArgumentNullException(nameof(positionChanged));

        var cursorDispatcher = (CursorDispatcher)DispatchCursor;
        var captureDispatcher = (CaptureDispatcher)DispatchCapture;
        _cursorDispatcherHandle = GCHandle.Alloc(cursorDispatcher, GCHandleType.Normal);
        _captureDispatcherHandle = GCHandle.Alloc(captureDispatcher, GCHandleType.Normal);

        try
        {
            _source = library.CreateExtImageSource(
                registry.ExtOutputSourceManager,
                output.Proxy,
                protocol.ExtCaptureSource);
            _cursorSession = library.CreateExtCursorSession(
                registry.ExtCopyManager,
                _source,
                pointer,
                protocol.ExtCursorSession);
            if (_cursorSession == IntPtr.Zero)
            {
                throw new InvalidOperationException("ext-image-copy cursor session creation returned NULL.");
            }

            _ = library.AddDispatcher(
                _cursorSession,
                Marshal.GetFunctionPointerForDelegate(cursorDispatcher));
            _captureSession = library.GetExtCursorCaptureSession(
                _cursorSession,
                protocol.ExtCopySession);
            if (_captureSession == IntPtr.Zero)
            {
                throw new InvalidOperationException("ext-image-copy cursor capture session creation returned NULL.");
            }

            _ = library.AddDispatcher(
                _captureSession,
                Marshal.GetFunctionPointerForDelegate(captureDispatcher));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Dispose();
            throw;
        }
    }

    private delegate int CursorDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);
    private delegate int CaptureDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    public bool IsReady => CaptureDone && !CaptureStopped && BufferWidth > 0 && BufferHeight > 0;
    public bool CaptureDone { get; private set; }
    public bool CaptureStopped { get; private set; }
    public uint BufferWidth { get; private set; }
    public uint BufferHeight { get; private set; }

    public void CaptureOutputGeneration()
    {
        _outputGeneration = _output.Generation;
        PublishPendingPosition();
    }

    internal static (int X, int Y)? MapCursorPosition(
        ScreenRect outputBounds,
        uint bufferWidth,
        uint bufferHeight,
        int bufferX,
        int bufferY)
    {
        if (bufferWidth is 0 ||
            bufferHeight is 0)
        {
            return null;
        }

        long logicalX = outputBounds.X + (long)Math.Floor(
            bufferX * (double)outputBounds.Width / bufferWidth);
        long logicalY = outputBounds.Y + (long)Math.Floor(
            bufferY * (double)outputBounds.Height / bufferHeight);
        return logicalX is >= int.MinValue and <= int.MaxValue &&
            logicalY is >= int.MinValue and <= int.MaxValue
            ? ((int)logicalX, (int)logicalY)
            : null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_captureSession != IntPtr.Zero)
        {
            _library.DestroyExtImageSession(_captureSession);
            _captureSession = IntPtr.Zero;
        }

        if (_cursorSession != IntPtr.Zero)
        {
            _library.DestroyExtCursorSession(_cursorSession);
            _cursorSession = IntPtr.Zero;
        }

        if (_source != IntPtr.Zero)
        {
            _library.DestroyExtImageSource(_source);
            _source = IntPtr.Zero;
        }

        if (_cursorDispatcherHandle.IsAllocated)
        {
            _cursorDispatcherHandle.Free();
        }

        if (_captureDispatcherHandle.IsAllocated)
        {
            _captureDispatcherHandle.Free();
        }
    }

    private int DispatchCursor(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        switch (opcode)
        {
            case 0:
                _entered = true;
                break;
            case 1:
                _entered = false;
                _hasPendingPosition = false;
                break;
            case 2 when _entered:
                var argumentSize = Marshal.SizeOf<WlArgument>();
                _pendingX = Marshal.PtrToStructure<WlArgument>(args).i;
                _pendingY = Marshal.PtrToStructure<WlArgument>(args + argumentSize).i;
                _hasPendingPosition = true;
                PublishPendingPosition();
                break;
        }

        return 0;
    }

    private int DispatchCapture(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (opcode is 0)
        {
            int argumentSize = Marshal.SizeOf<WlArgument>();
            BufferWidth = Marshal.PtrToStructure<WlArgument>(args).u;
            BufferHeight = Marshal.PtrToStructure<WlArgument>(args + argumentSize).u;
        }
        else if (opcode is 4)
        {
            CaptureDone = true;
            PublishPendingPosition();
        }
        else if (opcode is 5)
        {
            CaptureStopped = true;
        }

        return 0;
    }

    private void PublishPendingPosition()
    {
        if (!_entered ||
            !_hasPendingPosition ||
            !IsReady ||
            _outputGeneration is not { } outputGeneration ||
            outputGeneration != _output.Generation ||
            _output.ModeWidth <= 0 ||
            _output.ModeHeight <= 0)
        {
            return;
        }

        var outputBounds = new ScreenRect(_output.X, _output.Y, _output.ModeWidth, _output.ModeHeight);
        var position = MapCursorPosition(outputBounds, BufferWidth, BufferHeight, _pendingX, _pendingY);
        if (position is not null)
        {
            _positionChanged(position.Value.X, position.Value.Y);
        }
    }
}
