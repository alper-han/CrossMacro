
namespace CrossMacro.Platform.MacOS.Services;

public sealed class MacOSMousePositionProvider : IMousePositionProvider
{
    private readonly IMacOSCoreGraphicsNative _native;

    public MacOSMousePositionProvider()
        : this(new MacOSCoreGraphicsNative()) { /* Empty */ }

    internal MacOSMousePositionProvider(IMacOSCoreGraphicsNative native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public string ProviderName => "macOS CoreGraphics";
    public bool IsSupported => OperatingSystem.IsMacOS();

    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        var eventRef = CoreGraphics.CGEventCreate(IntPtr.Zero);
        if (eventRef == IntPtr.Zero)
        {
            return Task.FromResult<(int X, int Y)?>(null);
        }

        try
        {
            return Task.FromResult(ReadPosition(eventRef));
        }
        finally
        {
            CoreFoundation.CFRelease(eventRef);
        }
    }

    internal static (int X, int Y)? ReadPosition(IntPtr eventRef)
    {
        if (eventRef == IntPtr.Zero)
        {
            return null;
        }

        var loc = CoreGraphics.CGEventGetLocation(eventRef);
        return ((int)loc.X, (int)loc.Y);
    }

    public Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        var bounds = TryGetDesktopBounds();
        return Task.FromResult<(int Width, int Height)?>(bounds is not null
            ? (bounds.Value.Width, bounds.Value.Height)
            : null);
    }

    public Task<ScreenRect?> GetDesktopBoundsAsync()
    {
        return Task.FromResult(TryGetDesktopBounds());
    }

    private ScreenRect? TryGetDesktopBounds()
    {
        try
        {
            return CoreGraphicsMacOSScreenCaptureBackend.GetVirtualScreenBounds(_native);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trace.TraceWarning($"[MacOSMousePositionProvider] Failed to query virtual desktop bounds: {ex.Message}");
            return null;
        }
    }

    public void Dispose() { /* Empty */ }
}
