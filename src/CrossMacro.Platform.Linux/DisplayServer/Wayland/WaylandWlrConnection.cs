using CrossMacro.Platform.Abstractions;

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

    public static WaylandWlrConnection Connect()
    {
        var library = WaylandLibrary.Load();
        var display = library.DisplayConnect();
        if (display == IntPtr.Zero)
        {
            library.Dispose();
            throw new InvalidOperationException("wl_display_connect returned NULL.");
        }

        WaylandProtocolTables? protocol = null;
        try
        {
            protocol = new WaylandProtocolTables();
            var registryProxy = library.GetRegistry(display, protocol.WlRegistry);
            var registry = new WaylandRegistryState(library, protocol);
            library.AddDispatcher(registryProxy, registry.DispatcherPtr);
            for (var i = 0; i < RegistryRoundtripCount; i++)
            {
                library.DisplayRoundtrip(display);
            }

            registry.BindXdgOutputs();
            if (registry.XdgOutputManager != IntPtr.Zero)
            {
                library.DisplayRoundtrip(display);
            }

            return new WaylandWlrConnection(library, protocol, display, registry);
        }
        catch
        {
            protocol?.Dispose();
            library.DisplayDisconnect(display);
            library.Dispose();
            throw;
        }
    }

    public WlrScreencopyFrame Capture(ScreenRect? requestedRegion)
    {
        var output = SelectOutput(requestedRegion);
        var outputRegion = ToOutputRegion(output, requestedRegion);
        using var capture = new WaylandWlrRegionCapture(_library, _protocol, _display, Registry, output.Proxy);
        return capture.Capture(outputRegion, ToLogicalBounds(output, requestedRegion, outputRegion));
    }

    public ExtImageCopyFrame CaptureExtImageCopy(ScreenRect? requestedRegion)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var output = SelectOutput(requestedRegion);
        var outputRegion = ToOutputRegion(output, region: null);
        var capture = GetExtImageCopyCapture(output);
        return capture.Capture(ToLogicalBounds(output, requestedRegion: null, outputRegion));
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
            output.Dispose(_library);
        }

        _library.DisplayDisconnect(_display);
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

    private WaylandOutputInfo SelectOutput(ScreenRect? region)
    {
        if (Registry.Outputs.Count == 0)
        {
            throw new InvalidOperationException("Wayland registry did not expose any wl_output globals.");
        }

        if (region is null)
        {
            return Registry.Outputs[0];
        }

        return Registry.Outputs.FirstOrDefault(output => Contains(output, region.Value))
            ?? throw new ArgumentOutOfRangeException(nameof(region), region, "Wayland screen capture cannot capture a region that crosses or misses known Wayland outputs.");
    }

    private static bool Contains(WaylandOutputInfo output, ScreenRect region)
    {
        var width = output.ModeWidth;
        var height = output.ModeHeight;
        return width > 0 && height > 0 &&
            region.X >= output.X && region.Y >= output.Y &&
            region.Right <= checked(output.X + width) &&
            region.Bottom <= checked(output.Y + height);
    }

    private static ScreenRect ToOutputRegion(WaylandOutputInfo output, ScreenRect? region)
    {
        if (region is { } value)
        {
            return new ScreenRect(value.X - output.X, value.Y - output.Y, value.Width, value.Height);
        }

        if (output.ModeWidth <= 0 || output.ModeHeight <= 0)
        {
            throw new InvalidOperationException("Wayland output did not report a positive mode size.");
        }

        return new ScreenRect(0, 0, output.ModeWidth, output.ModeHeight);
    }

    private static ScreenRect ToLogicalBounds(WaylandOutputInfo output, ScreenRect? requestedRegion, ScreenRect outputRegion) =>
        requestedRegion ?? new ScreenRect(output.X, output.Y, outputRegion.Width, outputRegion.Height);
}
