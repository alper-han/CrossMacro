namespace CrossMacro.Platform.Abstractions.ScreenReading;

public interface IMacOSScreenRecordingPermissionProbe
{
    public bool IsPreflightAvailable { get; }

    public bool IsGranted();
}
