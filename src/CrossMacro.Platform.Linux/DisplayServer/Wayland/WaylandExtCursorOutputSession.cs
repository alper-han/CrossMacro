namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandExtCursorOutputSession : IDisposable
{
    private readonly WaylandLibrary _library;
    private readonly WaylandOutputInfo _output;
    private readonly Action<int, int> _positionChanged;
    private GCHandle _cursorDispatcherHandle;
    private GCHandle _cursorCaptureDispatcherHandle;
    private GCHandle _mainCaptureDispatcherHandle;
    private IntPtr _source;
    private IntPtr _cursorSession;
    private IntPtr _cursorCaptureSession;
    private IntPtr _mainCaptureSession;
    private bool _entered;
    private bool _hasPendingPosition;
    private int _pendingX;
    private int _pendingY;
    private bool _cursorCaptureStopped;
    private bool _mainCaptureDone;
    private bool _mainCaptureStopped;
    private uint _mainBufferWidth;
    private uint _mainBufferHeight;
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
        var cursorCaptureDispatcher = (CaptureDispatcher)DispatchCursorCapture;
        var mainCaptureDispatcher = (CaptureDispatcher)DispatchMainCapture;
        _cursorDispatcherHandle = GCHandle.Alloc(cursorDispatcher, GCHandleType.Normal);
        _cursorCaptureDispatcherHandle = GCHandle.Alloc(cursorCaptureDispatcher, GCHandleType.Normal);
        _mainCaptureDispatcherHandle = GCHandle.Alloc(mainCaptureDispatcher, GCHandleType.Normal);

        try
        {
            _source = library.CreateExtImageSource(
                registry.ExtOutputSourceManager,
                output.Proxy,
                protocol.ExtCaptureSource);
            // Cursor positions are expressed in the main output buffer's pixel space.
            // This metadata-only session provides that buffer size; no frame is captured.
            _mainCaptureSession = library.CreateExtImageSession(
                registry.ExtCopyManager,
                _source,
                protocol.ExtCopySession);
            if (_mainCaptureSession == IntPtr.Zero)
            {
                throw new InvalidOperationException("ext-image-copy output capture session creation returned NULL.");
            }

            _ = library.AddDispatcher(
                _mainCaptureSession,
                Marshal.GetFunctionPointerForDelegate(mainCaptureDispatcher));
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
            _cursorCaptureSession = library.GetExtCursorCaptureSession(
                _cursorSession,
                protocol.ExtCopySession);
            if (_cursorCaptureSession == IntPtr.Zero)
            {
                throw new InvalidOperationException("ext-image-copy cursor capture session creation returned NULL.");
            }

            _ = library.AddDispatcher(
                _cursorCaptureSession,
                Marshal.GetFunctionPointerForDelegate(cursorCaptureDispatcher));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Dispose();
            throw;
        }
    }

    private delegate int CursorDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);
    private delegate int CaptureDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    public bool IsReady =>
        _mainCaptureDone &&
        !_mainCaptureStopped &&
        _mainBufferWidth > 0 &&
        _mainBufferHeight > 0;

    public bool CaptureStopped => _cursorCaptureStopped || _mainCaptureStopped;

    public void CaptureOutputGeneration()
    {
        _outputGeneration = _output.Generation;
        PublishPendingPosition();
    }

    internal static (int X, int Y)? MapCursorPosition(
        ScreenRect outputBounds,
        uint mainBufferWidth,
        uint mainBufferHeight,
        int bufferX,
        int bufferY)
    {
        if (mainBufferWidth is 0 ||
            mainBufferHeight is 0)
        {
            return null;
        }

        long logicalX = outputBounds.X + (long)Math.Floor(
            bufferX * (double)outputBounds.Width / mainBufferWidth);
        long logicalY = outputBounds.Y + (long)Math.Floor(
            bufferY * (double)outputBounds.Height / mainBufferHeight);
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
        if (_cursorCaptureSession != IntPtr.Zero)
        {
            _library.DestroyExtImageSession(_cursorCaptureSession);
            _cursorCaptureSession = IntPtr.Zero;
        }

        if (_cursorSession != IntPtr.Zero)
        {
            _library.DestroyExtCursorSession(_cursorSession);
            _cursorSession = IntPtr.Zero;
        }

        if (_mainCaptureSession != IntPtr.Zero)
        {
            _library.DestroyExtImageSession(_mainCaptureSession);
            _mainCaptureSession = IntPtr.Zero;
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

        if (_cursorCaptureDispatcherHandle.IsAllocated)
        {
            _cursorCaptureDispatcherHandle.Free();
        }

        if (_mainCaptureDispatcherHandle.IsAllocated)
        {
            _mainCaptureDispatcherHandle.Free();
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

    private int DispatchCursorCapture(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (opcode is 5)
        {
            _cursorCaptureStopped = true;
        }

        return 0;
    }

    private int DispatchMainCapture(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        switch (opcode)
        {
            case 0:
                int argumentSize = Marshal.SizeOf<WlArgument>();
                _mainBufferWidth = Marshal.PtrToStructure<WlArgument>(args).u;
                _mainBufferHeight = Marshal.PtrToStructure<WlArgument>(args + argumentSize).u;
                _mainCaptureDone = false;
                break;
            case 4:
                _mainCaptureDone = true;
                PublishPendingPosition();
                break;
            case 5:
                _mainCaptureStopped = true;
                break;
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
        // The cursor capture session's buffer describes the cursor image itself.
        // Position events are expressed in the main output buffer's pixel space.
        var position = MapCursorPosition(
            outputBounds,
            _mainBufferWidth,
            _mainBufferHeight,
            _pendingX,
            _pendingY);
        if (position is not null)
        {
            _positionChanged(position.Value.X, position.Value.Y);
        }
    }
}
