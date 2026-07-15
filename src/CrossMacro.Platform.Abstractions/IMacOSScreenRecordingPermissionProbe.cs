namespace CrossMacro.Platform.Abstractions;

public interface IMacOSScreenRecordingPermissionProbe
{
    public bool IsPreflightAvailable { get; }

    public bool IsGranted();
}
