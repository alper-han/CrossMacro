namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandClipboardOffer : IDisposable
{
    private readonly WaylandClipboardConnection _connection;
    private readonly HashSet<string> _mimeTypes = new(StringComparer.OrdinalIgnoreCase);
    private GCHandle _dispatcherHandle;
    private bool _disposed;

    public WaylandClipboardOffer(WaylandClipboardConnection connection, IntPtr proxy)
    {
        _connection = connection;
        Proxy = proxy;
        var dispatcher = (OfferDispatcher)Dispatch;
        _dispatcherHandle = GCHandle.Alloc(dispatcher, GCHandleType.Normal);
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(dispatcher);
    }

    public IntPtr Proxy { get; }
    public IntPtr DispatcherPtr { get; }

    private delegate int OfferDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    internal void AddMimeType(string mimeType) => _mimeTypes.Add(mimeType);

    internal string? SelectTextMimeType()
    {
        foreach (var preferred in new[] { "text/plain;charset=utf-8", "text/plain", "UTF8_STRING", "TEXT", "STRING" })
        {
            var match = _mimeTypes.FirstOrDefault(mime => string.Equals(mime, preferred, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (opcode is 0)
        {
            var mimePointer = Marshal.PtrToStructure<WlArgument>(args).s;
            _connection.AddOfferMimeType(Proxy, Marshal.PtrToStringUTF8(mimePointer) ?? string.Empty);
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
