namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandCoreXdgSurface : IDisposable
{
    private readonly WaylandClipboardConnection _connection;
    private readonly IntPtr _proxy;
    private GCHandle _dispatcherHandle;
    private bool _disposed;

    public WaylandCoreXdgSurface(WaylandClipboardConnection connection, IntPtr proxy)
    {
        _connection = connection;
        _proxy = proxy;
        var dispatcher = (XdgSurfaceDispatcher)Dispatch;
        _dispatcherHandle = GCHandle.Alloc(dispatcher, GCHandleType.Normal);
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(dispatcher);
    }

    public IntPtr DispatcherPtr { get; }
    public bool IsConfigured { get; private set; }

    private delegate int XdgSurfaceDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (opcode is 0)
        {
            using var request = new WlArgumentPack(1);
            request[0] = new WlArgument { u = Marshal.PtrToStructure<WlArgument>(args).u };
            _ = _connection.Library.MarshalRequest(_proxy, 4, request);
            IsConfigured = true;
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
