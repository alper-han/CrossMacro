namespace CrossMacro.Platform.Linux.Services.Capabilities;

public readonly record struct LinuxBackendSelection(
    InputProviderMode Mode,
    bool CaptureSupported,
    string Reason)
{
    public LinuxInputBackend Backend { get; init; } = Mode switch
    {
        InputProviderMode.Daemon => LinuxInputBackend.Daemon,
        InputProviderMode.Legacy => LinuxInputBackend.DirectDevice,
        InputProviderMode.None => LinuxInputBackend.Unavailable,
        _ => LinuxInputBackend.Unavailable,
    };

    public bool IsSupported => Backend is not LinuxInputBackend.Unavailable;
}
