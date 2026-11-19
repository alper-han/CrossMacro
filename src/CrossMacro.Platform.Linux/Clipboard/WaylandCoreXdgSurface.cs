namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandCoreXdgSurface(WaylandClipboardConnection connection, IntPtr proxy) : WaylandProxyListener
{
    private readonly WaylandClipboardConnection _connection = connection;
    private readonly IntPtr _proxy = proxy;

    public bool IsConfigured { get; private set; }

    protected override void OnEvent(IntPtr target, uint opcode, IntPtr args)
    {
        if (opcode is 0)
        {
            using var request = new WlArgumentPack(1);
            request[0] = new WlArgument { u = Marshal.PtrToStructure<WlArgument>(args).u };
            _ = _connection.Library.MarshalRequest(_proxy, 4, request);
            IsConfigured = true;
        }
    }
}
