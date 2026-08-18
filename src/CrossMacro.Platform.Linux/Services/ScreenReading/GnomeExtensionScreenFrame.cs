
namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal sealed class GnomeExtensionScreenFrame(
    ScreenRect logicalBounds,
    int stride,
    ScreenPixelFormat pixelFormat,
    byte[] pixels,
    ScreenAlphaMode alphaMode) : IDisposable
{
    public ScreenRect LogicalBounds { get; } = logicalBounds;
    public int Stride { get; } = stride;
    public ScreenPixelFormat PixelFormat { get; } = pixelFormat;
    public byte[] Pixels { get; } = pixels;
    public ScreenAlphaMode AlphaMode { get; } = alphaMode;

    public void Dispose() { /* Empty */ }
}
