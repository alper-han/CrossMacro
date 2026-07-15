namespace CrossMacro.Platform.Linux.Services;

public readonly record struct LinuxBackendSelection(
    InputProviderMode Mode,
    bool CaptureSupported,
    string Reason)
{
    public bool IsSupported => string.Equals(Reason, "native-x11", StringComparison.Ordinal) || Mode is not InputProviderMode.None;
}
