namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandClipboardSource : IDisposable
{
    private readonly WaylandClipboardConnection _connection;
    private readonly byte[] _data;
    private readonly WaylandClipboardMode _mode;
    private GCHandle _dispatcherHandle;
    private bool _disposed;

    public WaylandClipboardSource(WaylandClipboardConnection connection, IntPtr proxy, byte[] data, WaylandClipboardMode mode)
    {
        _connection = connection;
        Proxy = proxy;
        _data = data;
        _mode = mode;
        var dispatcher = (SourceDispatcher)Dispatch;
        _dispatcherHandle = GCHandle.Alloc(dispatcher, GCHandleType.Normal);
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(dispatcher);
    }

    public IntPtr Proxy { get; }
    public IntPtr DispatcherPtr { get; }

    private delegate int SourceDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    public void OfferAll(IReadOnlyList<string> mimeTypes)
    {
        foreach (var mimeType in mimeTypes)
        {
            using var mime = new WlCString(mimeType);
            using var args = new WlArgumentPack(1);
            args[0] = new WlArgument { s = mime.Address };
            _ = _connection.SendRequest(Proxy, 0, args);
        }
    }

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        var argumentSize = Marshal.SizeOf<WlArgument>();
        if ((_mode is WaylandClipboardMode.Core && opcode is 1) || (_mode is not WaylandClipboardMode.Core && opcode is 0))
        {
            var fileDescriptor = Marshal.PtrToStructure<WlArgument>(args + argumentSize).h;
            WaylandClipboardConnection.HandleSourceSend(fileDescriptor, _data);
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
