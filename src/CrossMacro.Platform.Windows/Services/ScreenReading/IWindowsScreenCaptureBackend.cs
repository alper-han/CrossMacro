namespace CrossMacro.Platform.Windows.Services.ScreenReading;

internal interface IWindowsScreenCaptureBackend
{
    ScreenRect GetVirtualScreenBounds();

    WindowsScreenCaptureFrame Capture(ScreenRect region, CancellationToken cancellationToken);
}
