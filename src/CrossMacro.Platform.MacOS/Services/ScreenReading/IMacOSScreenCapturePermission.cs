namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal interface IMacOSScreenCapturePermission
{
    bool IsPreflightAvailable { get; }

    bool IsRequestAvailable { get; }

    bool Preflight();

    bool Request();
}
