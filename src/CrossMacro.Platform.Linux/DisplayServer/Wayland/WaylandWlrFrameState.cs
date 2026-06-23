using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandWlrFrameState
{
    private readonly FrameDispatcher _dispatcher;

    public WaylandWlrFrameState()
    {
        _dispatcher = Dispatch;
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(_dispatcher);
    }

    private delegate int FrameDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    public IntPtr DispatcherPtr { get; }
    public bool HasBuffer { get; private set; }
    public bool BufferDone { get; private set; }
    public bool Ready { get; private set; }
    public bool Failed { get; private set; }
    public uint Format { get; private set; }
    public uint Width { get; private set; }
    public uint Height { get; private set; }
    public uint Stride { get; private set; }
    public bool CanCreateBuffer => HasBuffer && BufferDone;

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        var size = Marshal.SizeOf<WlArgument>();
        switch (opcode)
        {
            case 0:
                Format = Marshal.PtrToStructure<WlArgument>(args).u;
                Width = Marshal.PtrToStructure<WlArgument>(args + size).u;
                Height = Marshal.PtrToStructure<WlArgument>(args + size * 2).u;
                Stride = Marshal.PtrToStructure<WlArgument>(args + size * 3).u;
                HasBuffer = true;
                break;
            case 2:
                Ready = true;
                break;
            case 3:
                Failed = true;
                break;
            case 6:
                BufferDone = true;
                break;
        }

        return 0;
    }
}

internal sealed class WaylandBufferState
{
    private readonly BufferDispatcher _dispatcher;

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
