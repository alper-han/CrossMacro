
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandRegistryState
{
    private readonly WaylandLibrary _library;
    private readonly WaylandProtocolTables _protocol;
#pragma warning disable S1450
    private readonly RegistryDispatcher _dispatcher;
#pragma warning restore S1450

    public WaylandRegistryState(WaylandLibrary library, WaylandProtocolTables protocol)
    {
        _library = library;
        _protocol = protocol;
        _dispatcher = Dispatch;
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(_dispatcher);
    }

    private delegate int RegistryDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    public IntPtr DispatcherPtr { get; }
    public List<WaylandOutputInfo> Outputs { get; } = [];
    public IntPtr Shm { get; private set; }
    public IntPtr XdgOutputManager { get; private set; }
    public IntPtr ExtOutputSourceManager { get; private set; }
    public IntPtr ExtCopyManager { get; private set; }
    public IntPtr WlrScreencopyManager { get; private set; }

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (opcode != 0)
        {
            return 0;
        }

        var size = Marshal.SizeOf<WlArgument>();
        var name = Marshal.PtrToStructure<WlArgument>(args).u;
        var ifacePointer = Marshal.PtrToStructure<WlArgument>(args + size).s;
        var version = Marshal.PtrToStructure<WlArgument>(args + (size * 2)).u;
        var iface = Marshal.PtrToStringUTF8(ifacePointer) ?? string.Empty;

        if (string.Equals(iface, "wl_output", StringComparison.Ordinal))
        {
            var output = new WaylandOutputInfo(name, _library.Bind(target, name, iface, Math.Min(version, 4), _protocol.WlOutput));
            _library.AddDispatcher(output.Proxy, output.DispatcherPtr);
            Outputs.Add(output);
        }
        else if (string.Equals(iface, "wl_shm", StringComparison.Ordinal))
        {
            Shm = _library.Bind(target, name, iface, Math.Min(version, 1), _protocol.WlShm);
        }
        else if (string.Equals(iface, "zwlr_screencopy_manager_v1", StringComparison.Ordinal))
        {
            WlrScreencopyManager = _library.Bind(target, name, iface, Math.Min(version, 3), _protocol.WlrScreencopyManager);
        }
        else if (string.Equals(iface, "zxdg_output_manager_v1", StringComparison.Ordinal))
        {
            XdgOutputManager = _library.Bind(target, name, iface, Math.Min(version, 3), _protocol.XdgOutputManager);
        }
        else if (string.Equals(iface, "ext_output_image_capture_source_manager_v1", StringComparison.Ordinal))
        {
            ExtOutputSourceManager = _library.Bind(target, name, iface, Math.Min(version, 1), _protocol.ExtOutputSourceManager);
        }
        else if (string.Equals(iface, "ext_image_copy_capture_manager_v1", StringComparison.Ordinal))
        {
            ExtCopyManager = _library.Bind(target, name, iface, Math.Min(version, 1), _protocol.ExtCopyManager);
        }

        return 0;
    }

    public void BindXdgOutputs()
    {
        if (XdgOutputManager == IntPtr.Zero)
        {
            return;
        }

        foreach (var output in Outputs)
        {
            if (output.XdgOutputProxy != IntPtr.Zero)
            {
                continue;
            }

            var xdgOutput = _library.GetXdgOutput(XdgOutputManager, output.Proxy, _protocol.XdgOutput);
            output.AttachXdgOutput(_library, xdgOutput);
        }
    }
}
