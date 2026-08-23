namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal interface IMacOSScreenCapturePermission
{
    public bool IsPreflightAvailable { get; }

    public bool IsRequestAvailable { get; }

    public bool Preflight();

    public bool Request();

    public void OpenSettings();
}
