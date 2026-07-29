
namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal sealed class GnomeExtensionScreenFrame(ScreenRect logicalBounds, int stride, ScreenPixelFormat pixelFormat, byte[] pixels) : IDisposable
{
    public ScreenRect LogicalBounds { get; } = logicalBounds;
    public int Stride { get; } = stride;
    public ScreenPixelFormat PixelFormat { get; } = pixelFormat;
    public byte[] Pixels { get; } = pixels;

    public void Dispose() { /* Empty */ }
}
