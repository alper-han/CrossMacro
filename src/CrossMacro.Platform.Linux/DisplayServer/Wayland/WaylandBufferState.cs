
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandBufferState
{
#pragma warning disable S1450
    private readonly BufferDispatcher _dispatcher;
#pragma warning restore S1450

    public WaylandBufferState()
    {
        _dispatcher = Dispatch;
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(_dispatcher);
    }

    private delegate int BufferDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    public IntPtr DispatcherPtr { get; }
    public bool Released { get; private set; } = true;

    public void MarkSubmitted() => Released = false;

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (opcode == 0)
        {
            Released = true;
        }

        return 0;
    }
}
