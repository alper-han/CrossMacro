
namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal sealed record MacOSCapturedImage(
    int Width,
    int Height,
    int BitsPerComponent,
    int BitsPerPixel,
    int BytesPerRow,
    CoreGraphics.CGBitmapInfo BitmapInfo,
    byte[] Pixels) : IDisposable
{
    public bool IsEmpty => Width is 0 || Height is 0 || Pixels.Length is 0;

    public void Dispose()
    {
    }
}
