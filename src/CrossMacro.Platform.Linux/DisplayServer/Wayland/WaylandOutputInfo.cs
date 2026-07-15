
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandOutputInfo
{
    private readonly OutputDispatcher _dispatcher;
    private readonly OutputDispatcher _xdgOutputDispatcher;

    public WaylandOutputInfo(uint globalName, IntPtr proxy)
    {
        GlobalName = globalName;
        Proxy = proxy;
        _dispatcher = Dispatch;
        _xdgOutputDispatcher = DispatchXdgOutput;
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(_dispatcher);
        XdgOutputDispatcherPtr = Marshal.GetFunctionPointerForDelegate(_xdgOutputDispatcher);
    }

    private delegate int OutputDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    public uint GlobalName { get; }
    public IntPtr Proxy { get; }
    public IntPtr XdgOutputProxy { get; private set; }
    public IntPtr DispatcherPtr { get; }
    public IntPtr XdgOutputDispatcherPtr { get; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public int ModeWidth { get; private set; }
    public int ModeHeight { get; private set; }

    public void AttachXdgOutput(WaylandLibrary library, IntPtr proxy)
    {
        XdgOutputProxy = proxy;
        library.AddDispatcher(proxy, XdgOutputDispatcherPtr);
    }

    public void Dispose(WaylandLibrary library)
    {
        if (XdgOutputProxy != IntPtr.Zero)
        {
            library.DestroyXdgOutput(XdgOutputProxy);
            XdgOutputProxy = IntPtr.Zero;
        }
    }

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        var size = Marshal.SizeOf<WlArgument>();
        if (opcode == 0)
        {
            X = Marshal.PtrToStructure<WlArgument>(args).i;
            Y = Marshal.PtrToStructure<WlArgument>(args + size).i;
        }
        else if (opcode == 1)
        {
            ModeWidth = Marshal.PtrToStructure<WlArgument>(args + size).i;
            ModeHeight = Marshal.PtrToStructure<WlArgument>(args + (size * 2)).i;
        }

        return 0;
    }

    private int DispatchXdgOutput(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        var size = Marshal.SizeOf<WlArgument>();
        if (opcode == 0)
        {
            X = Marshal.PtrToStructure<WlArgument>(args).i;
            Y = Marshal.PtrToStructure<WlArgument>(args + size).i;
        }
        else if (opcode == 1)
        {
            ModeWidth = Marshal.PtrToStructure<WlArgument>(args).i;
            ModeHeight = Marshal.PtrToStructure<WlArgument>(args + size).i;
        }

        return 0;
    }
}
