namespace CrossMacro.Platform.Linux.Services;

public readonly record struct LinuxBackendSelection(
    InputProviderMode Mode,
    bool CaptureSupported,
    string Reason)
{
    public bool IsSupported => Reason is "native-x11" || Mode is not InputProviderMode.None;
}
