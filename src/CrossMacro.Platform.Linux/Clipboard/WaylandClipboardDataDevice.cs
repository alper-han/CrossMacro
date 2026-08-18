namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandClipboardDataDevice : IDisposable
{
    private readonly WaylandClipboardConnection _connection;
    private readonly WaylandClipboardMode _mode;
    private GCHandle _dispatcherHandle;
    private bool _disposed;

    public WaylandClipboardDataDevice(WaylandClipboardConnection connection, IntPtr proxy, WaylandClipboardMode mode)
    {
        _connection = connection;
        Proxy = proxy;
        _mode = mode;
        var dispatcher = (DataDeviceDispatcher)Dispatch;
        _dispatcherHandle = GCHandle.Alloc(dispatcher, GCHandleType.Normal);
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(dispatcher);
    }

    public IntPtr Proxy { get; }
    public IntPtr DispatcherPtr { get; }

    private delegate int DataDeviceDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (_mode is WaylandClipboardMode.Core)
        {
            if (opcode is 0)
            {
                var offer = Marshal.PtrToStructure<WlArgument>(args).o;
                _connection.RegisterOffer(offer);
            }
            else if (opcode is 5)
            {
                _connection.SetCurrentOffer(Marshal.PtrToStructure<WlArgument>(args).o);
            }
        }
        else
        {
            if (opcode is 0)
            {
                var offer = Marshal.PtrToStructure<WlArgument>(args).o;
                _connection.RegisterOffer(offer);
            }
            else if (opcode is 1)
            {
                _connection.SetCurrentOffer(Marshal.PtrToStructure<WlArgument>(args).o);
            }
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
