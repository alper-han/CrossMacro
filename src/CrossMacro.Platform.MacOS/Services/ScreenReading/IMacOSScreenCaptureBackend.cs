using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal interface IMacOSScreenCaptureBackend
{
    ScreenRect GetVirtualScreenBounds();

    MacOSScreenCaptureFrame Capture(ScreenRect region, CancellationToken cancellationToken);
}
