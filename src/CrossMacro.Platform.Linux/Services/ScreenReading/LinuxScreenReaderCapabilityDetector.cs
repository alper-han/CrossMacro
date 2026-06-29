using System;
using System.Threading;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class LinuxScreenReaderCapabilityDetector : ILinuxScreenReaderCapabilityDetector
{
    private readonly IExtImageCopySupportProbe _extImageCopyProbe;
    private readonly IWlrScreencopySupportProbe _wlrScreencopyProbe;
    private readonly IPortalScreenCastSupportProbe _portalScreenCastProbe;
    private readonly IKWinScreenShotSupportProbe _kWinScreenShotProbe;
    private readonly GnomePositionProvider _gnomePositionProvider;

    private readonly Lazy<LinuxScreenReaderCapabilitySnapshot> _snapshot;

    public LinuxScreenReaderCapabilityDetector(GnomePositionProvider gnomePositionProvider)
        : this(
            WaylandExtImageCopySupportProbe.Instance,
            new WlrScreencopyCapture(),
            PortalScreenCastSupportProbe.Instance,
            new KWinScreenShotCapture(),
            gnomePositionProvider)
    {
    }

    public LinuxScreenReaderCapabilityDetector(
        IExtImageCopySupportProbe extImageCopyProbe,
        GnomePositionProvider gnomePositionProvider)
        : this(
            extImageCopyProbe,
            new WlrScreencopyCapture(),
            PortalScreenCastSupportProbe.Instance,
            new KWinScreenShotCapture(),
            gnomePositionProvider)
    {
    }

    public LinuxScreenReaderCapabilityDetector(
        IExtImageCopySupportProbe extImageCopyProbe,
        IWlrScreencopySupportProbe wlrScreencopyProbe,
        IPortalScreenCastSupportProbe portalScreenCastProbe,
        IKWinScreenShotSupportProbe kWinScreenShotProbe)
        : this(extImageCopyProbe, wlrScreencopyProbe, portalScreenCastProbe, kWinScreenShotProbe, new GnomePositionProvider())
    {
    }

    public LinuxScreenReaderCapabilityDetector(
        IExtImageCopySupportProbe extImageCopyProbe,
        IWlrScreencopySupportProbe wlrScreencopyProbe,
        IPortalScreenCastSupportProbe portalScreenCastProbe,
        IKWinScreenShotSupportProbe kWinScreenShotProbe,
        GnomePositionProvider gnomePositionProvider)
    {
        _extImageCopyProbe = extImageCopyProbe ?? throw new ArgumentNullException(nameof(extImageCopyProbe));
        _wlrScreencopyProbe = wlrScreencopyProbe ?? throw new ArgumentNullException(nameof(wlrScreencopyProbe));
        _portalScreenCastProbe = portalScreenCastProbe ?? throw new ArgumentNullException(nameof(portalScreenCastProbe));
        _kWinScreenShotProbe = kWinScreenShotProbe ?? throw new ArgumentNullException(nameof(kWinScreenShotProbe));
        _gnomePositionProvider = gnomePositionProvider ?? throw new ArgumentNullException(nameof(gnomePositionProvider));

        _snapshot = new Lazy<LinuxScreenReaderCapabilitySnapshot>(CreateSnapshot, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public LinuxScreenReaderCapabilitySnapshot GetSnapshot() => _snapshot.Value;

    private LinuxScreenReaderCapabilitySnapshot CreateSnapshot()
    {
        var extSupport = _extImageCopyProbe.ProbeSupport();
        var wlrSupport = _wlrScreencopyProbe.ProbeSupport();
        var portalSupport = _portalScreenCastProbe.ProbeSupport();
        var kWinSupport = _kWinScreenShotProbe.ProbeSupport();
        var isGnomeExtensionAvailable = _gnomePositionProvider.IsSupported && 
            _gnomePositionProvider.CurrentExtensionStatus?.Code == CrossMacro.Core.Services.ExtensionStatusCode.Enabled;

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
                    portalSupport.ErrorMessage ?? "XDG Desktop Portal ScreenCast is unavailable."),
            isGnomeExtensionAvailable
                ? LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.GnomeExtension)
                : LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.GnomeExtension,
                    ScreenReadErrorKind.BackendUnavailable,
                    "GNOME Shell extension backend is unavailable or not enabled."));
    }
}
