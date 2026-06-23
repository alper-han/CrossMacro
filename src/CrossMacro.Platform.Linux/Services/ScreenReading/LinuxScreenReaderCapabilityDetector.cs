using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class LinuxScreenReaderCapabilityDetector : ILinuxScreenReaderCapabilityDetector
{
    private readonly IExtImageCopySupportProbe _extImageCopyProbe;
    private readonly IWlrScreencopySupportProbe _wlrScreencopyProbe;
    private readonly IPortalScreenCastSupportProbe _portalScreenCastProbe;
    private readonly IKWinScreenShotSupportProbe _kWinScreenShotProbe;

    public LinuxScreenReaderCapabilityDetector()
        : this(
            WaylandExtImageCopySupportProbe.Instance,
            new WlrScreencopyCapture(),
            PortalScreenCastSupportProbe.Instance,
            new KWinScreenShotCapture())
    {
    }

    public LinuxScreenReaderCapabilityDetector(IExtImageCopySupportProbe extImageCopyProbe)
        : this(
            extImageCopyProbe,
            new WlrScreencopyCapture(),
            PortalScreenCastSupportProbe.Instance,
            new KWinScreenShotCapture())
    {
    }

    public LinuxScreenReaderCapabilityDetector(
        IExtImageCopySupportProbe extImageCopyProbe,
        IWlrScreencopySupportProbe wlrScreencopyProbe,
        IPortalScreenCastSupportProbe portalScreenCastProbe,
        IKWinScreenShotSupportProbe kWinScreenShotProbe)
    {
        _extImageCopyProbe = extImageCopyProbe ?? throw new ArgumentNullException(nameof(extImageCopyProbe));
        _wlrScreencopyProbe = wlrScreencopyProbe ?? throw new ArgumentNullException(nameof(wlrScreencopyProbe));
        _portalScreenCastProbe = portalScreenCastProbe ?? throw new ArgumentNullException(nameof(portalScreenCastProbe));
        _kWinScreenShotProbe = kWinScreenShotProbe ?? throw new ArgumentNullException(nameof(kWinScreenShotProbe));
    }

    public LinuxScreenReaderCapabilitySnapshot GetSnapshot()
    {
        var extSupport = _extImageCopyProbe.ProbeSupport();
        var wlrSupport = _wlrScreencopyProbe.ProbeSupport();
        var portalSupport = _portalScreenCastProbe.ProbeSupport();
        var kWinSupport = _kWinScreenShotProbe.ProbeSupport();
        return new LinuxScreenReaderCapabilitySnapshot(
            kWinSupport.IsSupported
                ? LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2)
                : LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.KWinScreenShot2,
                    kWinSupport.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                    kWinSupport.ErrorMessage ?? "KDE KWin ScreenShot2 is unavailable."),
            extSupport.IsSupported
                ? LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy)
                : LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.ExtImageCopy,
                    extSupport.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                    extSupport.ErrorMessage ?? "ext-image-copy-capture-v1 is unavailable."),
            wlrSupport.IsSupported
                ? LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy)
                : LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.WlrScreencopy,
                    wlrSupport.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                    wlrSupport.ErrorMessage ?? "wlr-screencopy screen reading backend is unavailable."),
            portalSupport.IsSupported
                ? LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal)
                : LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.Portal,
                    portalSupport.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                    portalSupport.ErrorMessage ?? "XDG Desktop Portal ScreenCast is unavailable."));
    }
}
