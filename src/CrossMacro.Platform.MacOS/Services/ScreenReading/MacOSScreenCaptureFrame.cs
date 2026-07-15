
namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal sealed record class MacOSScreenCaptureFrame(
    ScreenRect LogicalBounds,
    int Stride,
    ScreenPixelFormat PixelFormat,
    byte[] Pixels);
