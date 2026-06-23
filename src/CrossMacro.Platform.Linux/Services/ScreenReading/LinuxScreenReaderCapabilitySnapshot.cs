namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public readonly record struct LinuxScreenReaderCapabilitySnapshot(
    LinuxScreenReaderBackendCapability KWinScreenShot2,
    LinuxScreenReaderBackendCapability ExtImageCopy,
    LinuxScreenReaderBackendCapability WlrScreencopy,
    LinuxScreenReaderBackendCapability Portal)
{
    public LinuxScreenReaderBackendCapability GetCapability(LinuxScreenReaderBackend backend) => backend switch
    {
        LinuxScreenReaderBackend.KWinScreenShot2 => KWinScreenShot2,
        LinuxScreenReaderBackend.ExtImageCopy => ExtImageCopy,
        LinuxScreenReaderBackend.WlrScreencopy => WlrScreencopy,
        LinuxScreenReaderBackend.Portal => Portal,
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unknown Linux screen reader backend.")
    };
}
