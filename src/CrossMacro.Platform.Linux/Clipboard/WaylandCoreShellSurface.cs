namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandCoreShellSurface : IDisposable
{
    private readonly WaylandLibrary _library;
    private readonly IntPtr _proxy;
    private GCHandle _dispatcherHandle;
    private bool _disposed;

    public WaylandCoreShellSurface(WaylandLibrary library, IntPtr proxy)
    {
        _library = library;
        _proxy = proxy;
        var dispatcher = (ShellSurfaceDispatcher)Dispatch;
        _dispatcherHandle = GCHandle.Alloc(dispatcher, GCHandleType.Normal);
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(dispatcher);
    }

    public IntPtr DispatcherPtr { get; }

    private delegate int ShellSurfaceDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (opcode is 0)
        {
            using var pong = new WlArgumentPack(1);
            pong[0] = new WlArgument { u = Marshal.PtrToStructure<WlArgument>(args).u };
            _ = _library.MarshalRequest(_proxy, 0, pong);
        }

        return 0;
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
    }
}
