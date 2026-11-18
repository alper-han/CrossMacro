
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandRegistryState : IDisposable
{
    private readonly WaylandLibrary _library;
    private readonly WaylandProtocolTables _protocol;
    private readonly WaylandGlobalCatalog _catalog = new();
    private readonly List<WaylandOutputInfo> _retiredOutputs = [];
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

    private delegate int SeatDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

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

    /// <summary>
    /// Changes whenever a global this client uses appears or disappears, or the seat capabilities change.
    /// </summary>
    public int Generation => Volatile.Read(ref _generation);

    /// <summary>
    /// Releases the xdg-output proxies of every output, including withdrawn ones. Call before disconnecting.
    /// </summary>
    public void DestroyOutputs()
    {
        foreach (var output in Outputs)
        {
            output.Destroy(_library);
        }

        foreach (var output in _retiredOutputs)
        {
            output.Destroy(_library);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var output in Outputs)
        {
            output.Dispose();
        }

        foreach (var output in _retiredOutputs)
        {
            output.Dispose();
        }

        if (_dispatcherHandle.IsAllocated)
        {
            _dispatcherHandle.Free();
        }

        if (_seatDispatcherHandle.IsAllocated)
        {
            _seatDispatcherHandle.Free();
        }
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
            if (xdgOutput != IntPtr.Zero)
            {
                output.AttachXdgOutput(_library, xdgOutput);
            }
            else
            {
                Log.Debug("[WaylandRegistryState] Failed to bind xdg output for global {GlobalName}.", output.GlobalName);
            }
        }
    }

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
    {
        switch (opcode)
        {
            case 0:
                HandleGlobal(target, args);
                break;
            case 1:
                HandleGlobalRemove(args);
                break;
        }

        return 0;
    }

    private void HandleGlobal(IntPtr registry, IntPtr args)
    {
        var size = Marshal.SizeOf<WlArgument>();
        var name = Marshal.PtrToStructure<WlArgument>(args).u;
        var ifacePointer = Marshal.PtrToStructure<WlArgument>(args + size).s;
        var version = Marshal.PtrToStructure<WlArgument>(args + (size * 2)).u;
        var iface = Marshal.PtrToStringUTF8(ifacePointer) ?? string.Empty;

        switch (WaylandGlobalCatalog.Resolve(iface, version, out var slot, out var bindVersion))
        {
            case WaylandGlobalMatch.VersionTooOld:
                Log.Warning(
                    "[WaylandRegistryState] Ignoring {Interface} global {GlobalName}: version {Version} is older than this client supports.",
                    iface,
                    name,
                    version);
                break;
            case WaylandGlobalMatch.Bindable:
                if (BindGlobal(registry, name, iface, bindVersion, slot))
                {
                    _ = Interlocked.Increment(ref _generation);
                }

                break;
        }
    }

    private bool BindGlobal(IntPtr registry, uint name, string iface, uint bindVersion, WaylandGlobalSlot slot)
    {
        if (slot is not WaylandGlobalSlot.Output && GetSlot(slot) != IntPtr.Zero)
        {
            return false;
        }

        var proxy = _library.Bind(registry, name, iface, bindVersion, GetInterface(slot));
        if (proxy == IntPtr.Zero)
        {
            Log.Debug("[WaylandRegistryState] Failed to bind {Interface} global {GlobalName}.", iface, name);
            return false;
        }

        if (slot is WaylandGlobalSlot.Output)
        {
            var output = new WaylandOutputInfo(name, proxy);
            _ = _library.AddDispatcher(output.Proxy, output.DispatcherPtr);
            Outputs.Add(output);
            return true;
        }

        SetSlot(slot, proxy);
        _catalog.RecordSingleton(name, slot);
        if (slot is WaylandGlobalSlot.Seat)
        {
            _ = _library.AddDispatcher(proxy, SeatDispatcherPtr);
        }

        return true;
    }

    private void HandleGlobalRemove(IntPtr args)
    {
        var name = Marshal.PtrToStructure<WlArgument>(args).u;
        var removed = RetireOutput(name);
        if (_catalog.TryReleaseSingleton(name, out var slot))
        {
            var proxy = GetSlot(slot);
            SetSlot(slot, IntPtr.Zero);
            _library.DestroyProxy(proxy);
            removed = true;
        }

        if (removed)
        {
            _ = Interlocked.Increment(ref _generation);
        }
    }

    /// <summary>
    /// Takes a withdrawn output out of <see cref="Outputs"/>. Its proxy and dispatcher stay alive until
    /// disposal, because a capture that is already running may still reference it.
    /// </summary>
    private bool RetireOutput(uint name)
    {
        var index = Outputs.FindIndex(output => output.GlobalName == name);
        if (index < 0)
        {
            return false;
        }

        var output = Outputs[index];
        Outputs.RemoveAt(index);
        output.Destroy(_library);
        _retiredOutputs.Add(output);
        return true;
    }

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

    private IntPtr GetSlot(WaylandGlobalSlot slot) => slot switch
    {
        WaylandGlobalSlot.Seat => Seat,
        WaylandGlobalSlot.Shm => Shm,
        WaylandGlobalSlot.XdgOutputManager => XdgOutputManager,
        WaylandGlobalSlot.ExtOutputSourceManager => ExtOutputSourceManager,
        WaylandGlobalSlot.ExtCopyManager => ExtCopyManager,
        WaylandGlobalSlot.WlrScreencopyManager => WlrScreencopyManager,
        WaylandGlobalSlot.Output => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Outputs do not fill a singleton slot."),
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown Wayland global slot."),
    };

    private void SetSlot(WaylandGlobalSlot slot, IntPtr proxy)
    {
        switch (slot)
        {
            case WaylandGlobalSlot.Seat:
                Seat = proxy;
                if (proxy == IntPtr.Zero)
                {
                    SeatCapabilities = 0;
                }

                break;
            case WaylandGlobalSlot.Shm:
                Shm = proxy;
                break;
            case WaylandGlobalSlot.XdgOutputManager:
                XdgOutputManager = proxy;
                break;
            case WaylandGlobalSlot.ExtOutputSourceManager:
                ExtOutputSourceManager = proxy;
                break;
            case WaylandGlobalSlot.ExtCopyManager:
                ExtCopyManager = proxy;
                break;
            case WaylandGlobalSlot.WlrScreencopyManager:
                WlrScreencopyManager = proxy;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "The global does not fill a singleton slot.");
        }
    }

    private WaylandInterfaceHandle GetInterface(WaylandGlobalSlot slot) => slot switch
    {
        WaylandGlobalSlot.Output => _protocol.WlOutput,
        WaylandGlobalSlot.Seat => _protocol.WlSeat,
        WaylandGlobalSlot.Shm => _protocol.WlShm,
        WaylandGlobalSlot.XdgOutputManager => _protocol.XdgOutputManager,
        WaylandGlobalSlot.ExtOutputSourceManager => _protocol.ExtOutputSourceManager,
        WaylandGlobalSlot.ExtCopyManager => _protocol.ExtCopyManager,
        WaylandGlobalSlot.WlrScreencopyManager => _protocol.WlrScreencopyManager,
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown Wayland global slot."),
    };
}
