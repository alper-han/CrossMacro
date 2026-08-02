namespace CrossMacro.Platform.Linux.Clipboard;

/// <summary>
/// Produces a short-lived keyboard focus serial for the core Wayland data
/// device protocol. Data-control protocols do not need this surface; it is
/// only used by compositors that expose wl_data_device without a data-control
/// extension.
/// </summary>
internal sealed class WaylandCoreClipboardSurface : IDisposable
{
    private static readonly TimeSpan FocusTimeout = TimeSpan.FromSeconds(5);
    private readonly WaylandClipboardConnection _connection;
    private readonly WaylandLibrary _library;
    private readonly WaylandClipboardProtocol _protocol;
    private readonly WaylandClipboardRegistry _registry;
    private WaylandCoreClipboardKeyboard? _keyboard;
    private WaylandCoreXdgSurface? _xdgSurfaceHandler;
    private WaylandCoreShellSurface? _shellSurfaceHandler;
    private WaylandShmBuffer? _shmBuffer;
    private IntPtr _surface;
    private IntPtr _xdgSurface;
    private IntPtr _xdgToplevel;
    private IntPtr _shellSurface;
    private IntPtr _buffer;
    private bool _disposed;

    private WaylandCoreClipboardSurface(WaylandClipboardConnection connection)
    {
        _connection = connection;
        _library = connection.Library;
        _protocol = connection.Protocol;
        _registry = connection.Registry;
    }

    public static WaylandCoreClipboardSurface Create(
        WaylandClipboardConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        cancellationToken.ThrowIfCancellationRequested();

        var surface = new WaylandCoreClipboardSurface(connection);
        try
        {
            surface.Initialize(cancellationToken);
            return surface;
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    public uint WaitForKeyboardFocus(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var keyboard = _keyboard ?? throw new InvalidOperationException("Wayland core clipboard keyboard was not initialized.");
        if (keyboard.HasFocus)
        {
            return keyboard.FocusSerial;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(FocusTimeout);
        var dispatchCancellation = new WaylandCaptureCancellation(
            new ScreenReadOptions(cancellationToken: timeout.Token));
        try
        {
            while (!keyboard.HasFocus)
            {
                _library.DisplayDispatch(_connection.Display, dispatchCancellation);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "Wayland core clipboard could not obtain keyboard focus for a valid selection serial within the timeout.");
        }

        return keyboard.FocusSerial;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DestroyRoleObjects();
        _keyboard?.Dispose();
        _keyboard = null;
        _shmBuffer?.Dispose();
        _shmBuffer = null;
    }

    private void Initialize(CancellationToken cancellationToken)
    {
        if (!_registry.CoreClipboardSetSupported)
        {
            throw new InvalidOperationException(
                "Wayland core clipboard requires wl_compositor, wl_shm, a desktop shell, and a keyboard-capable seat.");
        }

        _surface = CreateSurface();
        _keyboard = CreateKeyboard(_surface);

        if (_registry.XdgWmBase != IntPtr.Zero)
        {
            InitializeXdgSurface(cancellationToken);
        }
        else
        {
            InitializeWlShellSurface();
        }

        CreateAndCommitTransparentBuffer(cancellationToken);
    }

    private WaylandCoreClipboardKeyboard CreateKeyboard(IntPtr surface)
    {
        using var args = new WlArgumentPack(1);
        args[0] = new WlArgument { o = IntPtr.Zero };
        var proxy = _library.MarshalConstructor(
            _registry.Seat,
            1,
            args,
            _protocol.WlKeyboard,
            Math.Min(_registry.SeatVersion, 7u));
        if (proxy == IntPtr.Zero)
        {
            throw new InvalidOperationException("Wayland seat failed to create a keyboard object.");
        }

        var keyboard = new WaylandCoreClipboardKeyboard(_library, proxy, surface, _registry.SeatVersion);
        _ = _library.AddDispatcher(proxy, keyboard.DispatcherPtr);
        return keyboard;
    }

    private IntPtr CreateSurface()
    {
        using var args = new WlArgumentPack(1);
        args[0] = new WlArgument { o = IntPtr.Zero };
        var surface = _library.MarshalConstructor(
            _registry.WlCompositor,
            0,
            args,
            _protocol.WlSurface,
            1);
        if (surface == IntPtr.Zero)
        {
            throw new InvalidOperationException("Wayland compositor failed to create a clipboard focus surface.");
        }

        return surface;
    }

    private void InitializeXdgSurface(CancellationToken cancellationToken)
    {
        using (var args = new WlArgumentPack(2))
        {
            args[0] = new WlArgument { o = IntPtr.Zero };
            args[1] = new WlArgument { o = _surface };
            _xdgSurface = _library.MarshalConstructor(
                _registry.XdgWmBase,
                2,
                args,
                _protocol.XdgSurface,
                1);
        }

        if (_xdgSurface == IntPtr.Zero)
        {
            throw new InvalidOperationException("xdg_wm_base failed to create xdg_surface for clipboard focus.");
        }

        _xdgSurfaceHandler = new WaylandCoreXdgSurface(_connection, _xdgSurface);
        _ = _library.AddDispatcher(_xdgSurface, _xdgSurfaceHandler.DispatcherPtr);

        using (var args = new WlArgumentPack(1))
        {
            args[0] = new WlArgument { o = IntPtr.Zero };
            _xdgToplevel = _library.MarshalConstructor(
                _xdgSurface,
                1,
                args,
                _protocol.XdgToplevel,
                1);
        }

        if (_xdgToplevel == IntPtr.Zero)
        {
            throw new InvalidOperationException("xdg_surface failed to create a toplevel clipboard focus surface.");
        }

        using (var title = new WlCString("CrossMacro Clipboard"))
        using (var args = new WlArgumentPack(1))
        {
            args[0] = new WlArgument { s = title.Address };
            _ = _library.MarshalRequest(_xdgToplevel, 2, args);
        }

        using (var appId = new WlCString("io.github.alper_han.crossmacro"))
        using (var args = new WlArgumentPack(1))
        {
            args[0] = new WlArgument { s = appId.Address };
            _ = _library.MarshalRequest(_xdgToplevel, 3, args);
        }

        CommitSurface();
        var roundtrip = new WaylandCaptureCancellation(new ScreenReadOptions(cancellationToken: cancellationToken));
        _library.DisplayRoundtrip(_connection.Display, roundtrip);
        if (!_xdgSurfaceHandler.IsConfigured)
        {
            throw new InvalidOperationException("xdg_surface did not send an initial configure event.");
        }
    }

    private void InitializeWlShellSurface()
    {
        using var args = new WlArgumentPack(2);
        args[0] = new WlArgument { o = IntPtr.Zero };
        args[1] = new WlArgument { o = _surface };
        _shellSurface = _library.MarshalConstructor(
            _registry.WlShell,
            0,
            args,
            _protocol.WlShellSurface,
            1);
        if (_shellSurface == IntPtr.Zero)
        {
            throw new InvalidOperationException("wl_shell failed to create a clipboard focus shell surface.");
        }

        _shellSurfaceHandler = new WaylandCoreShellSurface(_library, _shellSurface);
        _ = _library.AddDispatcher(_shellSurface, _shellSurfaceHandler.DispatcherPtr);
        _ = _library.MarshalRequest(_shellSurface, 3, args: null);

        using var title = new WlCString("CrossMacro Clipboard");
        using var titleArgs = new WlArgumentPack(1);
        titleArgs[0] = new WlArgument { s = title.Address };
        _ = _library.MarshalRequest(_shellSurface, 8, titleArgs);
    }

    private void CreateAndCommitTransparentBuffer(CancellationToken cancellationToken)
    {
        const int width = 1;
        const int height = 1;
        const int stride = width * 4;
        const int size = stride * height;
        _shmBuffer = WaylandShmBuffer.Create(size);

        var shmPool = _library.CreateShmPool(
            _registry.WlShm,
            _shmBuffer.Fd,
            size,
            _protocol.WlShmPool);
        if (shmPool == IntPtr.Zero)
        {
            throw new InvalidOperationException("wl_shm failed to create a clipboard focus buffer pool.");
        }

        try
        {
            _buffer = _library.CreateBuffer(
                shmPool,
                width,
                height,
                stride,
                format: 0,
                _protocol.WlBuffer);
            if (_buffer == IntPtr.Zero)
            {
                throw new InvalidOperationException("wl_shm_pool failed to create a clipboard focus buffer.");
            }
        }
        finally
        {
            _library.DestroyShmPool(shmPool);
        }

        using (var attachArgs = new WlArgumentPack(3))
        {
            attachArgs[0] = new WlArgument { o = _buffer };
            attachArgs[1] = new WlArgument { i = 0 };
            attachArgs[2] = new WlArgument { i = 0 };
            _ = _library.MarshalRequest(_surface, 1, attachArgs);
        }

        using (var damageArgs = new WlArgumentPack(4))
        {
            damageArgs[0] = new WlArgument { i = 0 };
            damageArgs[1] = new WlArgument { i = 0 };
            damageArgs[2] = new WlArgument { i = width };
            damageArgs[3] = new WlArgument { i = height };
            _ = _library.MarshalRequest(_surface, 2, damageArgs);
        }

        CommitSurface();
        _ = _library.DisplayFlush(_connection.Display);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void CommitSurface() => _ = _library.MarshalRequest(_surface, 6, args: null);

    private void DestroyRoleObjects()
    {
        if (_xdgToplevel != IntPtr.Zero)
        {
            _ = _library.MarshalRequest(_xdgToplevel, 0, args: null, version: 1, flags: 1);
            _xdgToplevel = IntPtr.Zero;
        }

        if (_xdgSurface != IntPtr.Zero)
        {
            _xdgSurfaceHandler?.Dispose();
            _xdgSurfaceHandler = null;
            _ = _library.MarshalRequest(_xdgSurface, 0, args: null, version: 1, flags: 1);
            _xdgSurface = IntPtr.Zero;
        }

        if (_shellSurface != IntPtr.Zero)
        {
            _shellSurfaceHandler?.Dispose();
            _library.DestroyProxy(_shellSurface);
            _shellSurface = IntPtr.Zero;
        }

        if (_buffer != IntPtr.Zero)
        {
            _ = _library.MarshalRequest(_buffer, 0, args: null, version: 1, flags: 1);
            _buffer = IntPtr.Zero;
        }

        if (_surface != IntPtr.Zero)
        {
            _ = _library.MarshalRequest(_surface, 0, args: null, version: 1, flags: 1);
            _surface = IntPtr.Zero;
        }
    }
}
