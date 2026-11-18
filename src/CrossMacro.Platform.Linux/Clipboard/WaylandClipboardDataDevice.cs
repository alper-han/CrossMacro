namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandClipboardDataDevice(
    WaylandClipboardConnection connection,
    IntPtr proxy,
    WaylandClipboardMode mode) : WaylandProxyListener
{
    private readonly WaylandClipboardConnection _connection = connection;
    private readonly WaylandClipboardMode _mode = mode;

    public IntPtr Proxy { get; } = proxy;

    protected override void OnEvent(uint opcode, IntPtr args)
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
    }
}
