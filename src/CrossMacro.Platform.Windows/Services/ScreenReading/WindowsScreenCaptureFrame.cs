namespace CrossMacro.Platform.Windows.Services.ScreenReading;

internal sealed record class WindowsScreenCaptureFrame(
    ScreenRect LogicalBounds,
    int Stride,
    ScreenPixelFormat PixelFormat,
    byte[] Pixels);
