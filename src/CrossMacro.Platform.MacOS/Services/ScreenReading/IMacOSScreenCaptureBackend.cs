
namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal interface IMacOSScreenCaptureBackend
{
    public ScreenRect GetVirtualScreenBounds();

    public MacOSScreenCaptureFrame Capture(ScreenRect region, CancellationToken cancellationToken);
}
