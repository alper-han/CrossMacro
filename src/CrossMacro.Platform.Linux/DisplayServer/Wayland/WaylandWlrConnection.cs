
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandWlrConnection : IDisposable
{
    private const int RegistryRoundtripCount = 2;
    private readonly WaylandLibrary _library;
    private readonly WaylandProtocolTables _protocol;
    private readonly IntPtr _display;
    private readonly Dictionary<uint, WaylandExtImageCopyOutputCapture> _extImageCopyCaptures = [];
    private bool _disposed;

    private WaylandWlrConnection(
        WaylandLibrary library,
        WaylandProtocolTables protocol,
        IntPtr display,
        WaylandRegistryState registry)
    {
        _library = library;
        _protocol = protocol;
        _display = display;
        Registry = registry;
    }

    public WaylandRegistryState Registry { get; }

    public static WaylandWlrConnection Connect(ScreenReadOptions options = default)
    {
        var cancellation = new WaylandCaptureCancellation(options);
        cancellation.ThrowIfCancellationRequested();
        var library = WaylandLibrary.Load();
        var display = library.DisplayConnect();
        if (display == IntPtr.Zero)
        {
            library.Dispose();
            throw new InvalidOperationException("wl_display_connect returned NULL.");
        }

        WaylandProtocolTables? protocol = null;
        WaylandRegistryState? registry = null;
        try
        {
            protocol = new WaylandProtocolTables();
            var registryProxy = library.GetRegistry(display, protocol.WlRegistry);
            registry = new WaylandRegistryState(library, protocol);
            _ = library.AddDispatcher(registryProxy, registry.DispatcherPtr);
            for (var i = 0; i < RegistryRoundtripCount; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                library.DisplayRoundtrip(display, cancellation);
            }

            registry.BindXdgOutputs();
            if (registry.XdgOutputManager != IntPtr.Zero)
            {
                cancellation.ThrowIfCancellationRequested();
                library.DisplayRoundtrip(display, cancellation);
            }

            return new WaylandWlrConnection(library, protocol, display, registry);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            protocol?.Dispose();
            library.DisplayDisconnect(display);
            registry?.Dispose();
            library.Dispose();
            throw;
        }
    }

    public WlrScreencopyFrame Capture(ScreenRect? requestedRegion, ScreenReadOptions options)
    {
        var cancellation = new WaylandCaptureCancellation(options);
        cancellation.ThrowIfCancellationRequested();
        var composedFrame = ComposeOutputs(requestedRegion, cancellation, static (connection, composer, output, intersection, captureCancellation) =>
        {
            captureCancellation.ThrowIfCancellationRequested();
            var outputRegion = ToOutputRegion(output, intersection);
            using var capture = new WaylandWlrRegionCapture(connection._library, connection._protocol, connection._display, connection.Registry, output.Proxy);
            using var frame = capture.Capture(outputRegion, intersection, captureCancellation);
            composer.CopySource(frame.Pixels.Span, frame.Stride, frame.PixelFormat, frame.PhysicalWidth, frame.PhysicalHeight, intersection, intersection);
        });

        try
        {
            return new WlrScreencopyFrame(
                composedFrame.LogicalBounds,
                composedFrame.Stride,
                composedFrame.PixelFormat,
                composedFrame.Pixels,
                composedFrame,
                composedFrame.ValidPixelMask,
                composedFrame.LogicalBounds.Width,
                composedFrame.LogicalBounds.Height,
                composedFrame.ValidityIndex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            composedFrame.Dispose();
            throw;
        }
    }

    public ExtImageCopyFrame CaptureExtImageCopy(ScreenRect? requestedRegion, ScreenReadOptions options)
    {
        var cancellation = new WaylandCaptureCancellation(options);
        cancellation.ThrowIfCancellationRequested();
        var composedFrame = ComposeOutputs(requestedRegion, cancellation, static (connection, composer, output, intersection, captureCancellation) =>
        {
            captureCancellation.ThrowIfCancellationRequested();
            var fullOutputBounds = ToOutputBounds(output);
            using var frame = connection.GetExtImageCopyCapture(output).Capture(fullOutputBounds, captureCancellation);
            composer.CopySource(frame.Pixels.Span, frame.Stride, frame.PixelFormat, frame.PhysicalWidth, frame.PhysicalHeight, fullOutputBounds, intersection);
        });

        try
        {
            return new ExtImageCopyFrame(
                composedFrame.LogicalBounds,
                composedFrame.Stride,
                composedFrame.PixelFormat,
                composedFrame.Pixels,
                composedFrame,
                composedFrame.ValidPixelMask,
                composedFrame.LogicalBounds.Width,
                composedFrame.LogicalBounds.Height,
                composedFrame.ValidityIndex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            composedFrame.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var capture in _extImageCopyCaptures.Values)
        {
            capture.Dispose();
        }

        _extImageCopyCaptures.Clear();
        foreach (var output in Registry.Outputs)
        {
            output.Destroy(_library);
        }

        _library.DisplayDisconnect(_display);
        Registry.Dispose();
        foreach (var output in Registry.Outputs)
        {
            output.Dispose();
        }
        _protocol.Dispose();
        _library.Dispose();
    }

    private WaylandExtImageCopyOutputCapture GetExtImageCopyCapture(WaylandOutputInfo output)
    {
        if (!_extImageCopyCaptures.TryGetValue(output.GlobalName, out var capture))
        {
            capture = new WaylandExtImageCopyOutputCapture(_library, _protocol, _display, Registry, output.Proxy);
            _extImageCopyCaptures.Add(output.GlobalName, capture);
        }

        return capture;
    }

    private WaylandComposedFrame ComposeOutputs(
        ScreenRect? requestedRegion,
        WaylandCaptureCancellation cancellation,
        Action<WaylandWlrConnection, WaylandScreenFrameComposer, WaylandOutputInfo, ScreenRect, WaylandCaptureCancellation> copyOutput)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var bounds = GetVirtualScreenBounds(requestedRegion);
        var outputs = GetIntersectingOutputs(bounds);
        if (outputs.Count is 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedRegion), requestedRegion, "Wayland screen capture requested region is outside all known Wayland outputs.");
        }

        using var composer = WaylandScreenFrameComposer.Create(bounds);
        foreach (var output in outputs)
        {
            cancellation.ThrowIfCancellationRequested();
            var intersection = Intersect(output, bounds);
            if (intersection is not { } outputIntersection)
            {
                continue;
            }

            copyOutput(this, composer, output, outputIntersection, cancellation);
        }

        return composer.Complete();
    }

    private ScreenRect GetVirtualScreenBounds(ScreenRect? requestedRegion)
    {
        if (requestedRegion is not null)
        {
            return requestedRegion.Value;
        }

        if (Registry.Outputs.Count is 0)
        {
            throw new InvalidOperationException("Wayland registry did not expose any wl_output globals.");
        }

        ScreenRect? bounds = null;
        foreach (var output in Registry.Outputs)
        {
            if (output.ModeWidth <= 0 || output.ModeHeight <= 0)
            {
                continue;
            }

            var outputBounds = ToOutputBounds(output);
            bounds = bounds is { } currentBounds
                ? WaylandScreenFrameComposer.Union(currentBounds, outputBounds)
                : outputBounds;
        }

        return bounds ?? throw new InvalidOperationException("Wayland outputs did not report any positive mode sizes.");
    }

    private List<WaylandOutputInfo> GetIntersectingOutputs(ScreenRect region)
    {
        var outputs = Registry.Outputs.Where(output => Intersect(output, region) != null).ToList();
        return outputs;
    }

    private static ScreenRect? Intersect(WaylandOutputInfo output, ScreenRect region)
    {
        if (output.ModeWidth <= 0 || output.ModeHeight <= 0)
        {
            return null;
        }

        return WaylandScreenFrameComposer.Intersect(ToOutputBounds(output), region);
    }

    private static ScreenRect ToOutputBounds(WaylandOutputInfo output) =>
        new(output.X, output.Y, output.ModeWidth, output.ModeHeight);

    private static ScreenRect ToOutputRegion(WaylandOutputInfo output, ScreenRect region)
    {
        return new ScreenRect(region.X - output.X, region.Y - output.Y, region.Width, region.Height);
    }
}
