namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandCoreShellSurface(WaylandLibrary library, IntPtr proxy) : WaylandProxyListener
{
    private readonly WaylandLibrary _library = library;
    private readonly IntPtr _proxy = proxy;

    protected override void OnEvent(IntPtr target, uint opcode, IntPtr args)
    {
        if (opcode is 0)
        {
            using var pong = new WlArgumentPack(1);
            pong[0] = new WlArgument { u = Marshal.PtrToStructure<WlArgument>(args).u };
            _ = _library.MarshalRequest(_proxy, 0, pong);
        }
    }
}
