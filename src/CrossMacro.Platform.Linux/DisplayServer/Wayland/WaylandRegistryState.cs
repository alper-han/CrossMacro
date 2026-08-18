
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandRegistryState : IDisposable
{
    private readonly WaylandLibrary _library;
    private readonly WaylandProtocolTables _protocol;
    private GCHandle _dispatcherHandle;
    private GCHandle _seatDispatcherHandle;
    private int _generation;
    private bool _disposed;

    public WaylandRegistryState(WaylandLibrary library, WaylandProtocolTables protocol)
    {
        _library = library;
        _protocol = protocol;
        var dispatcher = (RegistryDispatcher)Dispatch;
        _dispatcherHandle = GCHandle.Alloc(dispatcher, GCHandleType.Normal);
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(dispatcher);
        var seatDispatcher = (SeatDispatcher)DispatchSeat;
        _seatDispatcherHandle = GCHandle.Alloc(seatDispatcher, GCHandleType.Normal);
        SeatDispatcherPtr = Marshal.GetFunctionPointerForDelegate(seatDispatcher);
    }

    private delegate int RegistryDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    public IntPtr DispatcherPtr { get; }
    public IntPtr SeatDispatcherPtr { get; }
    public List<WaylandOutputInfo> Outputs { get; } = [];
    public IntPtr Shm { get; private set; }
    public IntPtr XdgOutputManager { get; private set; }
    public IntPtr ExtOutputSourceManager { get; private set; }
    public IntPtr ExtCopyManager { get; private set; }
    public IntPtr WlrScreencopyManager { get; private set; }
    public IntPtr Seat { get; private set; }
    public uint SeatCapabilities { get; private set; }
    public int Generation => Volatile.Read(ref _generation);

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

        if (_seatDispatcherHandle.IsAllocated)
        {
            _seatDispatcherHandle.Free();
        }
    }

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (opcode is 1)
        {
            _ = Interlocked.Increment(ref _generation);
            return 0;
        }

        if (opcode is not 0)
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
            _ = _library.AddDispatcher(output.Proxy, output.DispatcherPtr);
            Outputs.Add(output);
        }
        else if (string.Equals(iface, "wl_seat", StringComparison.Ordinal) && Seat == IntPtr.Zero)
        {
            Seat = _library.Bind(target, name, iface, Math.Min(version, 1), _protocol.WlSeat);
            _ = _library.AddDispatcher(Seat, SeatDispatcherPtr);
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

        _ = Interlocked.Increment(ref _generation);

        return 0;
    }

    private delegate int SeatDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    private int DispatchSeat(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (opcode is 0)
        {
            uint capabilities = Marshal.PtrToStructure<WlArgument>(args).u;
            if (SeatCapabilities != capabilities)
            {
                SeatCapabilities = capabilities;
                _ = Interlocked.Increment(ref _generation);
            }
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
