
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed partial class WaylandLibrary : IDisposable
{
    private static readonly string[] LibraryNames = ["libwayland-client.so.0", "libwayland-client.so"];
    private readonly IntPtr _handle;
    private readonly WlDisplayConnect _displayConnect;
    private readonly WlDisplayDisconnect _displayDisconnect;
    private readonly WlDisplayGetFd _displayGetFd;
    private readonly WlDisplayPrepareRead _displayPrepareRead;
    private readonly WlDisplayCancelRead _displayCancelRead;
    private readonly WlDisplayReadEvents _displayReadEvents;
    private readonly WlDisplayDispatchPending _displayDispatchPending;
    private readonly WlDisplayFlush _displayFlush;
    private readonly WlProxyMarshalArrayConstructorVersioned _marshalConstructor;
    private readonly WlProxyMarshalArrayFlags _marshalFlags;
    private readonly WlProxyDestroy _proxyDestroy;
    private readonly WlProxyAddDispatcher _addDispatcher;
    private bool _disposed;

    private WaylandLibrary(IntPtr handle)
    {
        _handle = handle;
        _displayConnect = Resolve<WlDisplayConnect>("wl_display_connect");
        _displayDisconnect = Resolve<WlDisplayDisconnect>("wl_display_disconnect");
        _displayGetFd = Resolve<WlDisplayGetFd>("wl_display_get_fd");
        _displayPrepareRead = Resolve<WlDisplayPrepareRead>("wl_display_prepare_read");
        _displayCancelRead = Resolve<WlDisplayCancelRead>("wl_display_cancel_read");
        _displayReadEvents = Resolve<WlDisplayReadEvents>("wl_display_read_events");
        _displayDispatchPending = Resolve<WlDisplayDispatchPending>("wl_display_dispatch_pending");
        _displayFlush = Resolve<WlDisplayFlush>("wl_display_flush");
        _marshalConstructor = Resolve<WlProxyMarshalArrayConstructorVersioned>("wl_proxy_marshal_array_constructor_versioned");
        _marshalFlags = Resolve<WlProxyMarshalArrayFlags>("wl_proxy_marshal_array_flags");
        _proxyDestroy = Resolve<WlProxyDestroy>("wl_proxy_destroy");
        _addDispatcher = Resolve<WlProxyAddDispatcher>("wl_proxy_add_dispatcher");
    }

    private delegate IntPtr WlDisplayConnect(IntPtr name);
    private delegate void WlDisplayDisconnect(IntPtr display);
    private delegate int WlDisplayGetFd(IntPtr display);
    private delegate int WlDisplayPrepareRead(IntPtr display);
    private delegate void WlDisplayCancelRead(IntPtr display);
    private delegate int WlDisplayReadEvents(IntPtr display);
    private delegate int WlDisplayDispatchPending(IntPtr display);
    private delegate int WlDisplayFlush(IntPtr display);
    private delegate IntPtr WlProxyMarshalArrayConstructorVersioned(IntPtr proxy, uint opcode, IntPtr args, IntPtr iface, uint version);
    private delegate IntPtr WlProxyMarshalArrayFlags(IntPtr proxy, uint opcode, IntPtr iface, uint version, uint flags, IntPtr args);
    private delegate void WlProxyDestroy(IntPtr proxy);
    private delegate int WlProxyAddDispatcher(IntPtr proxy, IntPtr dispatcherFunc, IntPtr dispatcherData, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int FileDescriptor;
        public short Events;
        public short Revents;
    }

    [LibraryImport("libc.so.6", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int poll(ref PollFd fds, IntPtr count, int timeout);

    private const short PollIn = 0x001;
    private const short PollOut = 0x004;
    private const short PollError = 0x008;
    private const short PollHangup = 0x010;
    private const int ErrnoInterrupted = 4;
    private const int ErrnoWouldBlock = 11;

    public static WaylandLibrary Load()
    {
        var handle = NativeLibraryLoader.Load(LibraryNames, "Wayland client library");
        try
        {
            return new WaylandLibrary(handle);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            NativeLibrary.Free(handle);
            throw;
        }
    }

    public IntPtr DisplayConnect() => _displayConnect(IntPtr.Zero);
    public void DisplayDisconnect(IntPtr display) => _displayDisconnect(display);
    public void DisplayRoundtrip(IntPtr display, WaylandCaptureCancellation cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var callback = CreateSyncCallback(display);
        try
        {
            while (!callback.Done)
            {
                DispatchInterruptibly(display, cancellation);
            }
        }
        finally
        {
            if (callback.Proxy != IntPtr.Zero)
            {
                _proxyDestroy(callback.Proxy);
            }

            callback.Dispose();
        }
    }

    public void DisplayDispatch(IntPtr display, WaylandCaptureCancellation cancellation) =>
        DispatchInterruptibly(display, cancellation);
    public int AddDispatcher(IntPtr proxy, IntPtr dispatcherPtr) => _addDispatcher(proxy, dispatcherPtr, IntPtr.Zero, IntPtr.Zero);
    public IntPtr GetRegistry(IntPtr display, WaylandInterfaceHandle registryInterface)
    {
        using var args = new WlArgumentPack(1);
        args[0] = new WlArgument { o = IntPtr.Zero };
        return _marshalConstructor(display, 1, args.Address, registryInterface.Address, 1);
    }

    private SyncCallback CreateSyncCallback(IntPtr display)
    {
        using var args = new WlArgumentPack(1);
        args[0] = new WlArgument { o = IntPtr.Zero };
        var callback = new SyncCallback();
        try
        {
            callback.Proxy = _marshalConstructor(display, 0, args.Address, callback.InterfaceAddress, 1);
            if (callback.Proxy == IntPtr.Zero)
            {
                throw new InvalidOperationException("wl_display.sync returned NULL.");
            }

            _ = AddDispatcher(callback.Proxy, callback.DispatcherPtr);
            return callback;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (callback.Proxy != IntPtr.Zero)
            {
                _proxyDestroy(callback.Proxy);
            }

            callback.Dispose();
            throw;
        }
    }

    private void DispatchInterruptibly(IntPtr display, WaylandCaptureCancellation cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var pending = _displayDispatchPending(display);
        if (pending < 0)
        {
            throw new IOException($"wl_display_dispatch_pending failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }

        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            if (_displayPrepareRead(display) is 0)
            {
                if (TryReadEvents(display, cancellation))
                {
                    DispatchPending(display);
                    return;
                }

                continue;
            }

            DispatchPending(display);
        }
    }

    private bool TryReadEvents(IntPtr display, WaylandCaptureCancellation cancellation)
    {
        var readEvents = false;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            if (!WaitForDisplayEvents(display, cancellation))
            {
                return false;
            }

            if (_displayReadEvents(display) < 0)
            {
                throw new IOException($"wl_display_read_events failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
            }

            readEvents = true;
            return true;
        }
        finally
        {
            if (!readEvents)
            {
                _displayCancelRead(display);
            }
        }
    }

    private bool WaitForDisplayEvents(IntPtr display, WaylandCaptureCancellation cancellation)
    {
        var flushResult = _displayFlush(display);
        if (flushResult < 0 && Marshal.GetLastPInvokeError() != ErrnoWouldBlock)
        {
            throw new IOException($"wl_display_flush failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }

        var pollFd = new PollFd
        {
            FileDescriptor = _displayGetFd(display),
            Events = (short)(PollIn | (flushResult < 0 ? PollOut : 0)),
        };
        if (pollFd.FileDescriptor < 0)
        {
            throw new IOException($"wl_display_get_fd failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }

        var result = poll(ref pollFd, new IntPtr(1), cancellation.GetPollTimeoutMilliseconds());
        if (result < 0)
        {
            if (Marshal.GetLastPInvokeError() == ErrnoInterrupted)
            {
                return false;
            }

            throw new IOException($"poll on Wayland display failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }

        if (result is 0)
        {
            return false;
        }

        if ((pollFd.Revents & (PollError | PollHangup)) is not 0)
        {
            throw new IOException("Wayland display connection closed while waiting for events.");
        }

        return (pollFd.Revents & PollIn) is not 0;
    }

    private void DispatchPending(IntPtr display)
    {
        if (_displayDispatchPending(display) < 0)
        {
            throw new IOException($"wl_display_dispatch_pending failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private sealed class SyncCallback : IDisposable
    {
        private GCHandle _dispatcherHandle;
        private readonly WaylandInterfaceHandle _interface;
        private bool _disposed;

        public SyncCallback()
        {
            var dispatcher = (CallbackDispatcher)Dispatch;
            _dispatcherHandle = GCHandle.Alloc(dispatcher, GCHandleType.Normal);
            var destructorDoneEvent = ("done", "u", true);
            _interface = new("wl_callback", 1, [], [(destructorDoneEvent.Item1, destructorDoneEvent.Item2)]);
            DispatcherPtr = Marshal.GetFunctionPointerForDelegate(dispatcher);
        }

        public IntPtr Proxy { get; set; }
        public IntPtr DispatcherPtr { get; }
        public IntPtr InterfaceAddress => _interface.Address;
        public bool Done { get; private set; }

        private delegate int CallbackDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

        private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
        {
            if (opcode == 0)
            {
                Done = true;
                Proxy = IntPtr.Zero;
            }

            return 0;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _interface.Dispose();
            if (_dispatcherHandle.IsAllocated)
            {
                _dispatcherHandle.Free();
            }
        }
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

    public IntPtr CreateExtImageFrame(IntPtr session, WaylandInterfaceHandle frameInterface)
    {
        using var args = new WlArgumentPack(1);
        args[0] = new WlArgument { o = IntPtr.Zero };
        args[0] = new WlArgument { o = IntPtr.Zero };
        return _marshalConstructor(session, 0, args.Address, frameInterface.Address, 1);
    }

    public void AttachExtImageFrameBuffer(IntPtr frame, IntPtr buffer)
    {
        using var args = new WlArgumentPack(1);
        args[0] = new WlArgument { o = buffer };
        _ = _marshalFlags(frame, 1, IntPtr.Zero, 1, 0, args.Address);
    }

    public void DamageExtImageFrameBuffer(IntPtr frame, int x, int y, int width, int height)
    {
        using var args = new WlArgumentPack(4);
        args[0] = new WlArgument { i = x };
        args[1] = new WlArgument { i = y };
        args[2] = new WlArgument { i = width };
        args[3] = new WlArgument { i = height };
        _ = _marshalFlags(frame, 2, IntPtr.Zero, 1, 0, args.Address);
    }

    public void CaptureExtImageFrame(IntPtr frame) => _marshalFlags(frame, 3, IntPtr.Zero, 1, 0, IntPtr.Zero);

    public IntPtr WlrCaptureOutputRegion(IntPtr manager, IntPtr output, ScreenRect region, WaylandInterfaceHandle frameInterface)
    {
        using var args = new WlArgumentPack(7);
        args[0] = new WlArgument { o = IntPtr.Zero };
        args[1] = new WlArgument { i = 0 };
        args[2] = new WlArgument { o = output };
        args[3] = new WlArgument { i = region.X };
        args[4] = new WlArgument { i = region.Y };
        args[5] = new WlArgument { i = region.Width };
        args[6] = new WlArgument { i = region.Height };
        return _marshalConstructor(manager, 1, args.Address, frameInterface.Address, 3);
    }

    public void WlrFrameCopy(IntPtr frame, IntPtr buffer)
    {
        using var args = new WlArgumentPack(1);
        args[0] = new WlArgument { o = buffer };
        _ = _marshalFlags(frame, 0, IntPtr.Zero, 1, 0, args.Address);
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
