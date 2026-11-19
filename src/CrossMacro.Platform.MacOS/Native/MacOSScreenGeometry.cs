namespace CrossMacro.Platform.MacOS.Native;

internal static class MacOSScreenGeometry
{
    internal static CoreGraphics.CGRect ToCGRect(ScreenRect rect) => new()
    {
        origin = new CoreGraphics.CGPoint { X = rect.X, Y = rect.Y },
        size = new CoreGraphics.CGSize { width = rect.Width, height = rect.Height },
    };

    /// <summary>Converts to whole pixels, growing fractional edges outwards so the result covers the source rectangle.</summary>
    internal static ScreenRect ToScreenRect(CoreGraphics.CGRect rect) => new(
        checked((int)Math.Floor(rect.origin.X)),
        checked((int)Math.Floor(rect.origin.Y)),
        checked((int)Math.Ceiling(rect.size.width)),
        checked((int)Math.Ceiling(rect.size.height)));
}
