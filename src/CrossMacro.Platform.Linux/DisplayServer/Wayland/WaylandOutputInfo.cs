
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandOutputInfo : IDisposable
{
    private GCHandle _dispatcherHandle;
    private GCHandle _xdgOutputDispatcherHandle;
    private int _geometryX;
    private int _geometryY;
    private int _modeWidth;
    private int _modeHeight;
    private int _scale = 1;
    private int _transform;
    private int _logicalX;
    private int _logicalY;
    private int _logicalWidth;
    private int _logicalHeight;
    private int _generation;
    private bool _hasLogicalPosition;
    private bool _hasLogicalSize;
    private bool _disposed;

    public WaylandOutputInfo(uint globalName, IntPtr proxy)
    {
        GlobalName = globalName;
        Proxy = proxy;
        var dispatcher = (OutputDispatcher)Dispatch;
        var xdgOutputDispatcher = (OutputDispatcher)DispatchXdgOutput;
        _dispatcherHandle = GCHandle.Alloc(dispatcher, GCHandleType.Normal);
        _xdgOutputDispatcherHandle = GCHandle.Alloc(xdgOutputDispatcher, GCHandleType.Normal);
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(dispatcher);
        XdgOutputDispatcherPtr = Marshal.GetFunctionPointerForDelegate(xdgOutputDispatcher);
    }

    private delegate int OutputDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    public uint GlobalName { get; }
    public IntPtr Proxy { get; }
    public IntPtr XdgOutputProxy { get; private set; }
    public IntPtr DispatcherPtr { get; }
    public IntPtr XdgOutputDispatcherPtr { get; }
    public int Generation => Volatile.Read(ref _generation);
    public int X => _hasLogicalPosition ? _logicalX : _geometryX;
    public int Y => _hasLogicalPosition ? _logicalY : _geometryY;
    public int ModeWidth => _hasLogicalSize
        ? _logicalWidth
        : ResolveFallbackLogicalSize(_modeWidth, _modeHeight, _scale, _transform).Width;
    public int ModeHeight => _hasLogicalSize
        ? _logicalHeight
        : ResolveFallbackLogicalSize(_modeWidth, _modeHeight, _scale, _transform).Height;

    public void AttachXdgOutput(WaylandLibrary library, IntPtr proxy)
    {
        XdgOutputProxy = proxy;
        _ = library.AddDispatcher(proxy, XdgOutputDispatcherPtr);
    }

    public void Destroy(WaylandLibrary library)
    {
        if (XdgOutputProxy != IntPtr.Zero)
        {
            library.DestroyXdgOutput(XdgOutputProxy);
            XdgOutputProxy = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_dispatcherHandle.IsAllocated)
        {
            _dispatcherHandle.Free();
        }

        if (_xdgOutputDispatcherHandle.IsAllocated)
        {
            _xdgOutputDispatcherHandle.Free();
        }
    }

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        var size = Marshal.SizeOf<WlArgument>();
        if (opcode == 0)
        {
            int geometryX = Marshal.PtrToStructure<WlArgument>(args).i;
            int geometryY = Marshal.PtrToStructure<WlArgument>(args + size).i;
            int transform = Marshal.PtrToStructure<WlArgument>(args + (size * 7)).i;
            if (_geometryX != geometryX || _geometryY != geometryY || _transform != transform)
            {
                _geometryX = geometryX;
                _geometryY = geometryY;
                _transform = transform;
                MarkChanged();
            }
        }
        else if (opcode == 1)
        {
            uint flags = Marshal.PtrToStructure<WlArgument>(args).u;
            if ((flags & 1u) is not 0 || _modeWidth <= 0 || _modeHeight <= 0)
            {
                int modeWidth = Marshal.PtrToStructure<WlArgument>(args + size).i;
                int modeHeight = Marshal.PtrToStructure<WlArgument>(args + (size * 2)).i;
                if (_modeWidth != modeWidth || _modeHeight != modeHeight)
                {
                    _modeWidth = modeWidth;
                    _modeHeight = modeHeight;
                    MarkChanged();
                }
            }
        }
        else if (opcode == 3)
        {
            int scale = Math.Max(1, Marshal.PtrToStructure<WlArgument>(args).i);
            if (_scale != scale)
            {
                _scale = scale;
                MarkChanged();
            }
        }

        return 0;
    }

    private int DispatchXdgOutput(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        var size = Marshal.SizeOf<WlArgument>();
        if (opcode == 0)
        {
            int logicalX = Marshal.PtrToStructure<WlArgument>(args).i;
            int logicalY = Marshal.PtrToStructure<WlArgument>(args + size).i;
            if (!_hasLogicalPosition || _logicalX != logicalX || _logicalY != logicalY)
            {
                _logicalX = logicalX;
                _logicalY = logicalY;
                _hasLogicalPosition = true;
                MarkChanged();
            }
        }
        else if (opcode == 1)
        {
            int logicalWidth = Marshal.PtrToStructure<WlArgument>(args).i;
            int logicalHeight = Marshal.PtrToStructure<WlArgument>(args + size).i;
            bool hasLogicalSize = logicalWidth > 0 && logicalHeight > 0;
            if (_logicalWidth != logicalWidth ||
                _logicalHeight != logicalHeight ||
                _hasLogicalSize != hasLogicalSize)
            {
                _logicalWidth = logicalWidth;
                _logicalHeight = logicalHeight;
                _hasLogicalSize = hasLogicalSize;
                MarkChanged();
            }
        }

        return 0;
    }

    internal static (int Width, int Height) ResolveFallbackLogicalSize(
        int modeWidth,
        int modeHeight,
        int scale,
        int transform)
    {
        if (modeWidth <= 0 || modeHeight <= 0)
        {
            return (0, 0);
        }

        int normalizedScale = Math.Max(1, scale);
        bool swapsAxes = transform is 1 or 3 or 5 or 7;
        int transformedWidth = swapsAxes ? modeHeight : modeWidth;
        int transformedHeight = swapsAxes ? modeWidth : modeHeight;
        return (
            DivideRoundUp(transformedWidth, normalizedScale),
            DivideRoundUp(transformedHeight, normalizedScale));
    }

    private static int DivideRoundUp(int value, int divisor) =>
        checked((int)(((long)value + divisor - 1L) / divisor));

    private void MarkChanged() => _ = Interlocked.Increment(ref _generation);
}
