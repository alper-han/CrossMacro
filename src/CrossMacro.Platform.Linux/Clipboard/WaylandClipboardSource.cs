namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandClipboardSource(
    WaylandClipboardConnection connection,
    IntPtr proxy,
    byte[] data,
    WaylandClipboardMode mode) : WaylandProxyListener
{
    private readonly WaylandClipboardConnection _connection = connection;
    private readonly byte[] _data = data;
    private readonly WaylandClipboardMode _mode = mode;

    public IntPtr Proxy { get; } = proxy;

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

    protected override void OnEvent(uint opcode, IntPtr args)
    {
        var argumentSize = Marshal.SizeOf<WlArgument>();
        if ((_mode is WaylandClipboardMode.Core && opcode is 1) || (_mode is not WaylandClipboardMode.Core && opcode is 0))
        {
            var fileDescriptor = Marshal.PtrToStructure<WlArgument>(args + argumentSize).h;
            _connection.HandleSourceSend(fileDescriptor, _data);
        }
    }
}
