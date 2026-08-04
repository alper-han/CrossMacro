namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandClipboardConnection : IDisposable
{
    private const int RegistryRoundtripCount = 2;
    internal WaylandLibrary Library { get; }
    internal WaylandClipboardProtocol Protocol { get; }
    internal IntPtr Display { get; }
    internal WaylandClipboardRegistry Registry { get; }
    private readonly WaylandClipboardDataDevice _dataDevice;
    private readonly Dictionary<IntPtr, WaylandClipboardOffer> _offers = [];
    private readonly CancellationTokenSource _eventLoopCancellation = new();
    internal WaylandClipboardOffer? CurrentOffer { get; private set; }
    private WaylandClipboardSource? _source;
    private Task? _eventLoop;
    private bool _disposed;

    private WaylandClipboardConnection(
        WaylandLibrary library,
        WaylandClipboardProtocol protocol,
        IntPtr display,
        WaylandClipboardRegistry registry,
        WaylandClipboardMode mode,
        IntPtr manager,
        IntPtr seat,
        IntPtr dataDevice)
    {
        Library = library;
        Protocol = protocol;
        Display = display;
        Registry = registry;
        Mode = mode;
        Manager = manager;
        Seat = seat;
        _dataDevice = new WaylandClipboardDataDevice(this, dataDevice, mode);
        _ = Library.AddDispatcher(dataDevice, _dataDevice.DispatcherPtr);
    }

    public WaylandClipboardMode Mode { get; }
    public IntPtr Manager { get; }
    public IntPtr Seat { get; }
    public bool IsSupported =>
        Manager != IntPtr.Zero &&
        Seat != IntPtr.Zero &&
        (Mode is not WaylandClipboardMode.Core || Registry.CoreClipboardSetSupported);

    public static WaylandClipboardConnection Connect(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var library = WaylandLibrary.Load();
        var display = library.DisplayConnect();
        if (display == IntPtr.Zero)
        {
            library.Dispose();
            throw new InvalidOperationException("wl_display_connect returned NULL.");
        }

        WaylandClipboardProtocol? protocol = null;
        WaylandClipboardRegistry? registry = null;
        try
        {
            protocol = new WaylandClipboardProtocol();
            var registryProxy = library.GetRegistry(display, protocol.WlRegistry);
            registry = new WaylandClipboardRegistry(library, protocol);
            _ = library.AddDispatcher(registryProxy, registry.DispatcherPtr);
            var roundtrip = new WaylandCaptureCancellation(new ScreenReadOptions(cancellationToken: cancellationToken));
            for (var i = 0; i < RegistryRoundtripCount; i++)
            {
                library.DisplayRoundtrip(display, roundtrip);
            }

            if (registry.Seat == IntPtr.Zero)
            {
                throw new InvalidOperationException("Wayland registry did not expose wl_seat.");
            }

            var mode = WaylandClipboardMode.Core;
            if (registry.ExtDataControlManager != IntPtr.Zero)
            {
                mode = WaylandClipboardMode.ExtDataControl;
            }
            else if (registry.WlrDataControlManager != IntPtr.Zero)
            {
                mode = WaylandClipboardMode.WlrDataControl;
            }

            if (mode is WaylandClipboardMode.Core && registry.WlDataDeviceManager == IntPtr.Zero)
            {
                throw new InvalidOperationException("Wayland registry did not expose a clipboard data-device manager.");
            }

            var manager = mode switch
            {
                WaylandClipboardMode.ExtDataControl => registry.ExtDataControlManager,
                WaylandClipboardMode.WlrDataControl => registry.WlrDataControlManager,
                WaylandClipboardMode.Core => registry.WlDataDeviceManager,
                _ => throw new InvalidOperationException("Unsupported Wayland clipboard mode."),
            };
            if (mode is WaylandClipboardMode.Core && !registry.CoreClipboardSetSupported)
            {
                throw new InvalidOperationException("Wayland core clipboard requires wl_compositor, wl_shm, a desktop shell, and a keyboard-capable seat.");
            }

            var dataDevice = CreateDataDevice(library, protocol, registry, mode, manager, registry.Seat);
            var connection = new WaylandClipboardConnection(
                library,
                protocol,
                display,
                registry,
                mode,
                manager,
                registry.Seat,
                dataDevice);
            library.DisplayRoundtrip(display, roundtrip);
            return connection;
        }
        catch
        {
            registry?.Dispose();
            protocol?.Dispose();
            library.DisplayDisconnect(display);
            library.Dispose();
            throw;
        }
    }

    public static async Task<string?> ReadTextAsync(CancellationToken cancellationToken)
    {
        using var connection = await Task.Run(
            () => Connect(cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return await connection.ReceiveTextAsync(cancellationToken).ConfigureAwait(false);
    }

    public void SetSelection(byte[] data, IReadOnlyList<string> mimeTypes, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        var source = CreateSource(data);
        try
        {
            source.OfferAll(mimeTypes);
            _source = source;
            if (Mode is WaylandClipboardMode.Core)
            {
                using var focusSurface = WaylandCoreClipboardSurface.Create(this, cancellationToken);
                var serial = focusSurface.WaitForKeyboardFocus(cancellationToken);
                SendSelection(source, serial);
            }
            else
            {
                SendSelection(source, serial: 0);
            }

            var roundtrip = new WaylandCaptureCancellation(new ScreenReadOptions(cancellationToken: cancellationToken));
            Library.DisplayRoundtrip(Display, roundtrip);
        }
        catch
        {
            if (ReferenceEquals(_source, source))
            {
                _source = null;
            }

            source.Dispose();
            throw;
        }
    }

    public void StartEventLoop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_eventLoop is not null)
        {
            return;
        }

        _eventLoop = Task.Run(EventLoop, _eventLoopCancellation.Token);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _eventLoopCancellation.Cancel();
        try
        {
            _eventLoop?.GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException)
        {
            Log.Debug(ex, "[WaylandNativeClipboard] Clipboard event loop stopped during disposal");
        }

        _eventLoopCancellation.Dispose();
        foreach (var offer in _offers.Values)
        {
            offer.Dispose();
        }

        _source?.Dispose();
        _dataDevice.Dispose();
        Library.DisplayDisconnect(Display);
        Registry.Dispose();
        Protocol.Dispose();
        Library.Dispose();
    }

    internal void RegisterOffer(IntPtr proxy)
    {
        if (_offers.ContainsKey(proxy))
        {
            return;
        }

        var offer = new WaylandClipboardOffer(this, proxy);
        _offers.Add(proxy, offer);
        _ = Library.AddDispatcher(proxy, offer.DispatcherPtr);
    }

    internal void SetCurrentOffer(IntPtr proxy)
    {
        CurrentOffer = proxy == IntPtr.Zero
            ? null
            : _offers.GetValueOrDefault(proxy);
    }

    internal void AddOfferMimeType(IntPtr proxy, string mimeType)
    {
        if (_offers.TryGetValue(proxy, out var offer))
        {
            offer.AddMimeType(mimeType);
        }
    }

    internal static void HandleSourceSend(int fileDescriptor, byte[] data)
    {
        _ = Task.Run(
            () => SendClipboardData(fileDescriptor, data),
            CancellationToken.None);
    }

    internal IntPtr SendRequest(IntPtr proxy, uint opcode, WlArgumentPack? args) =>
        Library.MarshalRequest(proxy, opcode, args);

    internal void Receive(IntPtr offerProxy, string mimeType, int writeFileDescriptor)
    {
        using var mime = new WlCString(mimeType);
        using var args = new WlArgumentPack(2);
        args[0] = new WlArgument { s = mime.Address };
        args[1] = new WlArgument { h = writeFileDescriptor };
        _ = Library.MarshalRequest(offerProxy, 0, args, 1);
        _ = Library.DisplayFlush(Display);
    }

    private static void SendClipboardData(int fileDescriptor, byte[] data)
    {
        try
        {
            using var handle = new SafeFileHandle((IntPtr)fileDescriptor, ownsHandle: true);
            LinuxFileDescriptorNative.WriteAll(fileDescriptor, data, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[WaylandNativeClipboard] Failed to send clipboard data");
        }
    }

    private WaylandClipboardSource CreateSource(byte[] data)
    {
        var sourceInterface = Mode switch
        {
            WaylandClipboardMode.ExtDataControl => Protocol.ExtDataControlSource,
            WaylandClipboardMode.WlrDataControl => Protocol.WlrDataControlSource,
            WaylandClipboardMode.Core => Protocol.WlDataSource,
            _ => throw new InvalidOperationException("Unsupported Wayland clipboard mode."),
        };
        using var args = new WlArgumentPack(1);
        args[0] = new WlArgument { o = IntPtr.Zero };
        var proxy = Library.MarshalConstructor(Manager, 0, args, sourceInterface, 1);
        if (proxy == IntPtr.Zero)
        {
            throw new InvalidOperationException("Wayland clipboard manager failed to create a data source.");
        }

        var source = new WaylandClipboardSource(this, proxy, data, Mode);
        _ = Library.AddDispatcher(proxy, source.DispatcherPtr);
        return source;
    }

    private void EventLoop()
    {
        var cancellation = new WaylandCaptureCancellation(new ScreenReadOptions(cancellationToken: _eventLoopCancellation.Token));
        try
        {
            while (!_eventLoopCancellation.IsCancellationRequested)
            {
                Library.DisplayDispatch(Display, cancellation);
            }
        }
        catch (OperationCanceledException) when (_eventLoopCancellation.IsCancellationRequested)
        {
            // Normal owner shutdown.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[WaylandNativeClipboard] Clipboard event loop failed");
        }
    }

    private void SendSelection(WaylandClipboardSource source, uint serial)
    {
        using var args = new WlArgumentPack(Mode is WaylandClipboardMode.Core ? 2 : 1);
        args[0] = new WlArgument { o = source.Proxy };
        if (Mode is WaylandClipboardMode.Core)
        {
            args[1] = new WlArgument { u = serial };
            _ = Library.MarshalRequest(_dataDevice.Proxy, 1, args);
        }
        else
        {
            _ = Library.MarshalRequest(_dataDevice.Proxy, 0, args);
        }
    }

    private async Task<string?> ReceiveTextAsync(CancellationToken cancellationToken)
    {
        var offer = CurrentOffer;
        if (offer is null)
        {
            return string.Empty;
        }

        var mimeType = offer.SelectTextMimeType();
        if (mimeType is null)
        {
            return string.Empty;
        }

        var (readFileDescriptor, writeFileDescriptor) = LinuxFileDescriptorNative.CreatePipe();
        try
        {
            Receive(offer.Proxy, mimeType, writeFileDescriptor);
        }
        finally
        {
            LinuxFileDescriptorNative.Close(writeFileDescriptor);
        }

        using var stream = new FileStream(
            new SafeFileHandle((IntPtr)readFileDescriptor, ownsHandle: true),
            FileAccess.Read,
            bufferSize: 16 * 1024,
            isAsync: false);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private static IntPtr CreateDataDevice(
        WaylandLibrary library,
        WaylandClipboardProtocol protocol,
        WaylandClipboardRegistry registry,
        WaylandClipboardMode mode,
        IntPtr manager,
        IntPtr seat)
    {
        using var args = new WlArgumentPack(2);
        args[0] = new WlArgument { o = IntPtr.Zero };
        args[1] = new WlArgument { o = seat };
        var deviceInterface = mode switch
        {
            WaylandClipboardMode.ExtDataControl => protocol.ExtDataControlDevice,
            WaylandClipboardMode.WlrDataControl => protocol.WlrDataControlDevice,
            WaylandClipboardMode.Core => protocol.WlDataDevice,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported Wayland clipboard mode."),
        };
        var version = mode switch
        {
            WaylandClipboardMode.ExtDataControl => 1u,
            WaylandClipboardMode.WlrDataControl => registry.WlrDataControlManagerVersion,
            WaylandClipboardMode.Core => registry.WlDataDeviceManagerVersion,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported Wayland clipboard mode."),
        };
        var proxy = library.MarshalConstructor(manager, 1, args, deviceInterface, version);
        return proxy == IntPtr.Zero
            ? throw new InvalidOperationException("Wayland clipboard manager failed to create a data device.")
            : proxy;
    }
}
