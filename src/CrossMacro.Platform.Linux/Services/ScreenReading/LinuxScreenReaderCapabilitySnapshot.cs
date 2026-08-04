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
    { /* Empty */ }

    public LinuxScreenReaderBackendCapability GetCapability(LinuxScreenReaderBackend backend) => backend switch
    {
        LinuxScreenReaderBackend.KWinScreenShot2 => KWinScreenShot2,
        LinuxScreenReaderBackend.ExtImageCopy => ExtImageCopy,
        LinuxScreenReaderBackend.WlrScreencopy => WlrScreencopy,
        LinuxScreenReaderBackend.Portal => Portal,
        LinuxScreenReaderBackend.GnomeExtension => GnomeExtension,
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unknown Linux screen reader backend."),
    };

    internal static LinuxScreenReaderCapabilitySnapshot NotApplicable(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new LinuxScreenReaderCapabilitySnapshot(
            Unavailable(LinuxScreenReaderBackend.KWinScreenShot2, reason),
            Unavailable(LinuxScreenReaderBackend.ExtImageCopy, reason),
            Unavailable(LinuxScreenReaderBackend.WlrScreencopy, reason),
            Unavailable(LinuxScreenReaderBackend.Portal, reason),
            Unavailable(LinuxScreenReaderBackend.GnomeExtension, reason));
    }

    private static LinuxScreenReaderBackendCapability Unavailable(
        LinuxScreenReaderBackend backend,
        string reason) =>
        LinuxScreenReaderBackendCapability.Unavailable(
            backend,
            ScreenReadErrorKind.Unsupported,
            reason);
}
