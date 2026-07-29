
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandOutputInfo : IDisposable
{
    private GCHandle _dispatcherHandle;
    private GCHandle _xdgOutputDispatcherHandle;
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
    public int X { get; private set; }
    public int Y { get; private set; }
    public int ModeWidth { get; private set; }
    public int ModeHeight { get; private set; }

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
            X = Marshal.PtrToStructure<WlArgument>(args).i;
            Y = Marshal.PtrToStructure<WlArgument>(args + size).i;
        }
        else if (opcode == 1)
        {
            ModeWidth = Marshal.PtrToStructure<WlArgument>(args + size).i;
            ModeHeight = Marshal.PtrToStructure<WlArgument>(args + (size * 2)).i;
        }

        return 0;
    }

    private int DispatchXdgOutput(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        var size = Marshal.SizeOf<WlArgument>();
        if (opcode == 0)
        {
            X = Marshal.PtrToStructure<WlArgument>(args).i;
            Y = Marshal.PtrToStructure<WlArgument>(args + size).i;
        }
        else if (opcode == 1)
        {
            ModeWidth = Marshal.PtrToStructure<WlArgument>(args).i;
            ModeHeight = Marshal.PtrToStructure<WlArgument>(args + size).i;
        }

        return 0;
    }
}
