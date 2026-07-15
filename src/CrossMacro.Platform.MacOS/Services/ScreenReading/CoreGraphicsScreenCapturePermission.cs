using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal sealed class CoreGraphicsScreenCapturePermission : IMacOSScreenCapturePermission
{
    public bool IsPreflightAvailable => CoreGraphics.IsCGPreflightScreenCaptureAccessAvailable();

    public bool IsRequestAvailable => CoreGraphics.IsCGRequestScreenCaptureAccessAvailable();

    public bool Preflight() => CoreGraphics.CGPreflightScreenCaptureAccess();

    public bool Request() => CoreGraphics.CGRequestScreenCaptureAccess();
}
