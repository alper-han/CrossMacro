namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandClipboardRegistry : IDisposable
{
    private readonly WaylandLibrary _library;
    private readonly WaylandClipboardProtocol _protocol;
    private GCHandle _dispatcherHandle;
    private GCHandle _seatDispatcherHandle;
    private GCHandle _xdgWmBaseDispatcherHandle;
    private bool _disposed;

    public WaylandClipboardRegistry(WaylandLibrary library, WaylandClipboardProtocol protocol)
    {
        _library = library;
        _protocol = protocol;
        var dispatcher = (RegistryDispatcher)Dispatch;
        _dispatcherHandle = GCHandle.Alloc(dispatcher, GCHandleType.Normal);
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(dispatcher);
        var seatDispatcher = (SeatDispatcher)DispatchSeat;
        _seatDispatcherHandle = GCHandle.Alloc(seatDispatcher, GCHandleType.Normal);
        SeatDispatcherPtr = Marshal.GetFunctionPointerForDelegate(seatDispatcher);
        var xdgWmBaseDispatcher = (XdgWmBaseDispatcher)DispatchXdgWmBase;
        _xdgWmBaseDispatcherHandle = GCHandle.Alloc(xdgWmBaseDispatcher, GCHandleType.Normal);
        XdgWmBaseDispatcherPtr = Marshal.GetFunctionPointerForDelegate(xdgWmBaseDispatcher);
    }

    private delegate int RegistryDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    public IntPtr DispatcherPtr { get; }
    public IntPtr SeatDispatcherPtr { get; }
    public IntPtr XdgWmBaseDispatcherPtr { get; }
    public IntPtr Seat { get; private set; }
    public uint SeatVersion { get; private set; }
    public uint SeatCapabilities { get; private set; }
    public uint WlDataDeviceManagerVersion { get; private set; }
    public uint WlrDataControlManagerVersion { get; private set; }
    public IntPtr WlCompositor { get; private set; }
    public IntPtr WlShm { get; private set; }
    public IntPtr XdgWmBase { get; private set; }
    public IntPtr WlShell { get; private set; }
    public IntPtr WlDataDeviceManager { get; private set; }
    public IntPtr ExtDataControlManager { get; private set; }
    public IntPtr WlrDataControlManager { get; private set; }

    public bool CoreClipboardSetSupported =>
        Seat != IntPtr.Zero &&
        (SeatCapabilities & 2u) is not 0 &&
        WlCompositor != IntPtr.Zero &&
        WlShm != IntPtr.Zero &&
        (XdgWmBase != IntPtr.Zero || WlShell != IntPtr.Zero);

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

        if (_xdgWmBaseDispatcherHandle.IsAllocated)
        {
            _xdgWmBaseDispatcherHandle.Free();
        }
    }

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (opcode is not 0)
        {
            return 0;
        }

        var argumentSize = Marshal.SizeOf<WlArgument>();
        var name = Marshal.PtrToStructure<WlArgument>(args).u;
        var interfacePointer = Marshal.PtrToStructure<WlArgument>(args + argumentSize).s;
        var version = Marshal.PtrToStructure<WlArgument>(args + (argumentSize * 2)).u;
        var interfaceName = Marshal.PtrToStringUTF8(interfacePointer) ?? string.Empty;
        switch (interfaceName)
        {
            case "wl_seat" when Seat == IntPtr.Zero:
                SeatVersion = Math.Min(version, 7u);
                Seat = _library.Bind(target, name, interfaceName, SeatVersion, _protocol.WlSeat);
                _ = _library.AddDispatcher(Seat, SeatDispatcherPtr);
                break;
            case "wl_compositor" when WlCompositor == IntPtr.Zero:
                WlCompositor = _library.Bind(target, name, interfaceName, Math.Min(version, 4u), _protocol.WlCompositor);
                break;
            case "wl_shm" when WlShm == IntPtr.Zero:
                WlShm = _library.Bind(target, name, interfaceName, 1, _protocol.WlShm);
                break;
            case "xdg_wm_base" when XdgWmBase == IntPtr.Zero:
                XdgWmBase = _library.Bind(target, name, interfaceName, 1, _protocol.XdgWmBase);
                _ = _library.AddDispatcher(XdgWmBase, XdgWmBaseDispatcherPtr);
                break;
            case "wl_shell" when WlShell == IntPtr.Zero:
                WlShell = _library.Bind(target, name, interfaceName, 1, _protocol.WlShell);
                break;
            case "wl_data_device_manager" when WlDataDeviceManager == IntPtr.Zero:
                WlDataDeviceManagerVersion = Math.Min(version, 3u);
                WlDataDeviceManager = _library.Bind(target, name, interfaceName, WlDataDeviceManagerVersion, _protocol.WlDataDeviceManager);
                break;
            case "ext_data_control_manager_v1" when ExtDataControlManager == IntPtr.Zero:
                ExtDataControlManager = _library.Bind(target, name, interfaceName, Math.Min(version, 1), _protocol.ExtDataControlManager);
                break;
            case "zwlr_data_control_manager_v1" when WlrDataControlManager == IntPtr.Zero:
                WlrDataControlManagerVersion = Math.Min(version, 2u);
                WlrDataControlManager = _library.Bind(target, name, interfaceName, WlrDataControlManagerVersion, _protocol.WlrDataControlManager);
                break;
        }

        return 0;
    }

    private delegate int SeatDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    private int DispatchSeat(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (opcode is 0)
        {
            SeatCapabilities = Marshal.PtrToStructure<WlArgument>(args).u;
        }

        return 0;
    }

    private delegate int XdgWmBaseDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    private int DispatchXdgWmBase(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        if (opcode is 0 && XdgWmBase != IntPtr.Zero)
        {
            using var request = new WlArgumentPack(1);
            request[0] = new WlArgument { u = Marshal.PtrToStructure<WlArgument>(args).u };
            _ = _library.MarshalRequest(XdgWmBase, 3, request);
        }

        return 0;
    }
}
