namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public readonly record struct LinuxScreenReaderCapabilitySnapshot(
    LinuxScreenReaderBackendCapability KWinScreenShot2,
    LinuxScreenReaderBackendCapability ExtImageCopy,
    LinuxScreenReaderBackendCapability WlrScreencopy,
    LinuxScreenReaderBackendCapability Portal,
    LinuxScreenReaderBackendCapability GnomeExtension)
{
    public LinuxScreenReaderCapabilitySnapshot(
        LinuxScreenReaderBackendCapability kWinScreenShot2,
        LinuxScreenReaderBackendCapability extImageCopy,
        LinuxScreenReaderBackendCapability wlrScreencopy,
        LinuxScreenReaderBackendCapability portal)
        : this(
            kWinScreenShot2,
            extImageCopy,
            wlrScreencopy,
            portal,
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.GnomeExtension,
                ScreenReadErrorKind.BackendUnavailable,
                "GNOME Shell extension backend is unavailable or not enabled."))
    {
    }

    public LinuxScreenReaderBackendCapability GetCapability(LinuxScreenReaderBackend backend) => backend switch
    {
        LinuxScreenReaderBackend.KWinScreenShot2 => KWinScreenShot2,
        LinuxScreenReaderBackend.ExtImageCopy => ExtImageCopy,
        LinuxScreenReaderBackend.WlrScreencopy => WlrScreencopy,
        LinuxScreenReaderBackend.Portal => Portal,
        LinuxScreenReaderBackend.GnomeExtension => GnomeExtension,
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unknown Linux screen reader backend.")
    };
}
