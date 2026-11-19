namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandWlrFrameState : WaylandProxyListener
{
    private const uint YInvertFlag = 1;

    public bool HasBuffer { get; private set; }
    public bool BufferDone { get; private set; }
    public bool Ready { get; private set; }
    public bool Failed { get; private set; }
    public uint Format { get; private set; }
    public uint Width { get; private set; }
    public uint Height { get; private set; }
    public uint Stride { get; private set; }
    public bool YInverted { get; private set; }
    public bool CanCreateBuffer => HasBuffer && BufferDone;

    protected override void OnEvent(IntPtr target, uint opcode, IntPtr args)
    {
        var size = Marshal.SizeOf<WlArgument>();
        switch (opcode)
        {
            case 0:
                Format = Marshal.PtrToStructure<WlArgument>(args).u;
                Width = Marshal.PtrToStructure<WlArgument>(args + size).u;
                Height = Marshal.PtrToStructure<WlArgument>(args + (size * 2)).u;
                Stride = Marshal.PtrToStructure<WlArgument>(args + (size * 3)).u;
                HasBuffer = true;
                break;
            case 1:
                YInverted = (Marshal.PtrToStructure<WlArgument>(args).u & YInvertFlag) is not 0;
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
    }
}
