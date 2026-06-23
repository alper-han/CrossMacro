using System.Runtime.InteropServices;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.Native;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandLibrary : IDisposable
{
    private static readonly string[] LibraryNames = ["libwayland-client.so.0", "libwayland-client.so"];
    private readonly IntPtr _handle;
    private readonly WlDisplayConnect _displayConnect;
    private readonly WlDisplayDisconnect _displayDisconnect;
    private readonly WlDisplayRoundtrip _displayRoundtrip;
    private readonly WlDisplayDispatch _displayDispatch;
    private readonly WlProxyMarshalArrayConstructorVersioned _marshalConstructor;
    private readonly WlProxyMarshalArrayFlags _marshalFlags;
    private readonly WlProxyAddDispatcher _addDispatcher;
    private bool _disposed;

    private WaylandLibrary(IntPtr handle)
    {
        _handle = handle;
        _displayConnect = Resolve<WlDisplayConnect>("wl_display_connect");
        _displayDisconnect = Resolve<WlDisplayDisconnect>("wl_display_disconnect");
        _displayRoundtrip = Resolve<WlDisplayRoundtrip>("wl_display_roundtrip");
        _displayDispatch = Resolve<WlDisplayDispatch>("wl_display_dispatch");
        _marshalConstructor = Resolve<WlProxyMarshalArrayConstructorVersioned>("wl_proxy_marshal_array_constructor_versioned");
        _marshalFlags = Resolve<WlProxyMarshalArrayFlags>("wl_proxy_marshal_array_flags");
        _addDispatcher = Resolve<WlProxyAddDispatcher>("wl_proxy_add_dispatcher");
    }

    private delegate IntPtr WlDisplayConnect(IntPtr name);
    private delegate void WlDisplayDisconnect(IntPtr display);
    private delegate int WlDisplayRoundtrip(IntPtr display);
    private delegate int WlDisplayDispatch(IntPtr display);
    private delegate IntPtr WlProxyMarshalArrayConstructorVersioned(IntPtr proxy, uint opcode, IntPtr args, IntPtr iface, uint version);
    private delegate IntPtr WlProxyMarshalArrayFlags(IntPtr proxy, uint opcode, IntPtr iface, uint version, uint flags, IntPtr args);
    private delegate int WlProxyAddDispatcher(IntPtr proxy, IntPtr dispatcherFunc, IntPtr dispatcherData, IntPtr data);

    public static WaylandLibrary Load()
    {
        var handle = NativeLibraryLoader.Load(LibraryNames, "Wayland client library");
        try
        {
            return new WaylandLibrary(handle);
        }
        catch
        {
            NativeLibrary.Free(handle);
            throw;
        }
    }

    public IntPtr DisplayConnect() => _displayConnect(IntPtr.Zero);
    public void DisplayDisconnect(IntPtr display) => _displayDisconnect(display);
    public int DisplayRoundtrip(IntPtr display) => _displayRoundtrip(display);
    public int DisplayDispatch(IntPtr display) => _displayDispatch(display);
    public int AddDispatcher(IntPtr proxy, IntPtr dispatcherPtr) => _addDispatcher(proxy, dispatcherPtr, IntPtr.Zero, IntPtr.Zero);
    public IntPtr GetRegistry(IntPtr display, WaylandInterfaceHandle registryInterface)
    {
        using var args = new WlArgumentPack(1);
        args[0] = new WlArgument { o = IntPtr.Zero };
        return _marshalConstructor(display, 1, args.Address, registryInterface.Address, 1);
    }

    public IntPtr Bind(IntPtr registry, uint name, string iface, uint version, WaylandInterfaceHandle targetInterface)
    {
        using var ifaceName = new WlCString(iface);
        using var args = new WlArgumentPack(3);
        args[0] = new WlArgument { u = name };
        args[1] = new WlArgument { s = ifaceName.Address };
        args[2] = new WlArgument { u = version };
        return _marshalConstructor(registry, 0, args.Address, targetInterface.Address, version);
    }

    public IntPtr CreateShmPool(IntPtr shm, int fd, int size, WaylandInterfaceHandle poolInterface)
    {
        using var args = new WlArgumentPack(3);
        args[0] = new WlArgument { o = IntPtr.Zero };
        args[1] = new WlArgument { h = fd };
        args[2] = new WlArgument { i = size };
        return _marshalConstructor(shm, 0, args.Address, poolInterface.Address, 1);
    }

    public IntPtr CreateBuffer(IntPtr pool, int width, int height, int stride, uint format, WaylandInterfaceHandle bufferInterface)
    {
        using var args = new WlArgumentPack(6);
        args[0] = new WlArgument { o = IntPtr.Zero };
        args[1] = new WlArgument { i = 0 };
        args[2] = new WlArgument { i = width };
        args[3] = new WlArgument { i = height };
        args[4] = new WlArgument { i = stride };
        args[5] = new WlArgument { u = format };
        return _marshalConstructor(pool, 0, args.Address, bufferInterface.Address, 1);
    }

    public IntPtr GetXdgOutput(IntPtr manager, IntPtr output, WaylandInterfaceHandle xdgOutputInterface)
    {
        using var args = new WlArgumentPack(2);
        args[0] = new WlArgument { o = IntPtr.Zero };
        args[1] = new WlArgument { o = output };
        return _marshalConstructor(manager, 1, args.Address, xdgOutputInterface.Address, 3);
    }

    public IntPtr CreateExtImageSource(IntPtr outputSourceManager, IntPtr output, WaylandInterfaceHandle sourceInterface)
    {
        using var args = new WlArgumentPack(2);
        args[0] = new WlArgument { o = IntPtr.Zero };
        args[1] = new WlArgument { o = output };
        return _marshalConstructor(outputSourceManager, 0, args.Address, sourceInterface.Address, 1);
    }

    public IntPtr CreateExtImageSession(IntPtr copyManager, IntPtr source, WaylandInterfaceHandle sessionInterface)
    {
        using var args = new WlArgumentPack(3);
        args[0] = new WlArgument { o = IntPtr.Zero };
        args[1] = new WlArgument { o = source };
        args[2] = new WlArgument { u = 0 };
        return _marshalConstructor(copyManager, 0, args.Address, sessionInterface.Address, 1);
    }

    public unsafe IntPtr CreateExtImageFrame(IntPtr session, WaylandInterfaceHandle frameInterface)
    {
        var args = stackalloc WlArgument[1];
        args[0] = new WlArgument { o = IntPtr.Zero };
        return _marshalConstructor(session, 0, (IntPtr)args, frameInterface.Address, 1);
    }

    public unsafe void AttachExtImageFrameBuffer(IntPtr frame, IntPtr buffer)
    {
        var args = stackalloc WlArgument[1];
        args[0] = new WlArgument { o = buffer };
        _marshalFlags(frame, 1, IntPtr.Zero, 1, 0, (IntPtr)args);
    }

    public unsafe void DamageExtImageFrameBuffer(IntPtr frame, int x, int y, int width, int height)
    {
        var args = stackalloc WlArgument[4];
        args[0] = new WlArgument { i = x };
        args[1] = new WlArgument { i = y };
        args[2] = new WlArgument { i = width };
        args[3] = new WlArgument { i = height };
        _marshalFlags(frame, 2, IntPtr.Zero, 1, 0, (IntPtr)args);
    }

    public void CaptureExtImageFrame(IntPtr frame) => _marshalFlags(frame, 3, IntPtr.Zero, 1, 0, IntPtr.Zero);

    public unsafe IntPtr WlrCaptureOutputRegion(IntPtr manager, IntPtr output, ScreenRect region, WaylandInterfaceHandle frameInterface)
    {
        var args = stackalloc WlArgument[7];
        args[0] = new WlArgument { o = IntPtr.Zero };
        args[1] = new WlArgument { i = 0 };
        args[2] = new WlArgument { o = output };
        args[3] = new WlArgument { i = region.X };
        args[4] = new WlArgument { i = region.Y };
        args[5] = new WlArgument { i = region.Width };
        args[6] = new WlArgument { i = region.Height };
        return _marshalConstructor(manager, 1, (IntPtr)args, frameInterface.Address, 3);
    }

    public unsafe void WlrFrameCopy(IntPtr frame, IntPtr buffer)
    {
        var args = stackalloc WlArgument[1];
        args[0] = new WlArgument { o = buffer };
        _marshalFlags(frame, 0, IntPtr.Zero, 1, 0, (IntPtr)args);
    }

    public void DestroyBuffer(IntPtr buffer) => _marshalFlags(buffer, 0, IntPtr.Zero, 1, 1, IntPtr.Zero);
    public void DestroyShmPool(IntPtr pool) => _marshalFlags(pool, 1, IntPtr.Zero, 1, 1, IntPtr.Zero);
    public void DestroyXdgOutput(IntPtr xdgOutput) => _marshalFlags(xdgOutput, 0, IntPtr.Zero, 1, 1, IntPtr.Zero);
    public void DestroyExtImageSource(IntPtr source) => _marshalFlags(source, 0, IntPtr.Zero, 1, 1, IntPtr.Zero);
    public void DestroyExtImageSession(IntPtr session) => _marshalFlags(session, 1, IntPtr.Zero, 1, 1, IntPtr.Zero);
    public void DestroyExtImageFrame(IntPtr frame) => _marshalFlags(frame, 0, IntPtr.Zero, 1, 1, IntPtr.Zero);
    public void DestroyWlrFrame(IntPtr frame) => _marshalFlags(frame, 1, IntPtr.Zero, 1, 1, IntPtr.Zero);

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            NativeLibrary.Free(_handle);
        }
    }

    private T Resolve<T>(string symbol) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_handle, symbol));
}
