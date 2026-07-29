namespace CrossMacro.Platform.Windows.Services.ScreenReading;

internal sealed record WindowsScreenCaptureFrame(
    ScreenRect LogicalBounds,
    int Stride,
    ScreenPixelFormat PixelFormat,
    byte[] Pixels);
