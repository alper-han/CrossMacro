namespace CrossMacro.Platform.Abstractions;

public interface IMacOSScreenRecordingPermissionProbe
{
    bool IsPreflightAvailable { get; }

    bool IsGranted();
}
