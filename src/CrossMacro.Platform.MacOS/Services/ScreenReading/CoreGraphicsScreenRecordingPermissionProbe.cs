using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal sealed class CoreGraphicsScreenRecordingPermissionProbe : IMacOSScreenRecordingPermissionProbe
{
    public bool IsPreflightAvailable => CoreGraphics.IsCGPreflightScreenCaptureAccessAvailable();

    public bool IsGranted() => CoreGraphics.CGPreflightScreenCaptureAccess();
}
