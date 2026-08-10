
namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal sealed record MacOSScreenCaptureFrame(
    ScreenRect LogicalBounds,
    int Stride,
    ScreenPixelFormat PixelFormat,
    byte[] Pixels,
    byte[]? ValidPixelMask = null);
