namespace CrossMacro.Platform.Windows.Services.ScreenReading;

internal interface IWindowsScreenCaptureBackend
{
    public ScreenRect GetVirtualScreenBounds();

    public WindowsScreenCaptureFrame Capture(ScreenRect region, CancellationToken cancellationToken);
}
