namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal static class LinuxScreenBackendDescriptors
{
    public static LinuxScreenBackendDescriptor Ext(IExtImageCopySupportProbe? probe = null, Func<ExtImageCopySupportResult, IScreenFrameProvider>? create = null) =>
        new(LinuxScreenReaderBackend.ExtImageCopy,
            () => ProbeExt(probe ?? throw new InvalidOperationException("Backend probe is not configured.")),
            capability => (create ?? throw new InvalidOperationException("Backend provider is not configured."))(
                capability.IsAvailable ? ExtImageCopySupportResult.Supported() : ExtImageCopySupportResult.Failure(
                    capability.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                    capability.ErrorMessage ?? "ext-image-copy-capture-v1 is unavailable.")));

    private static LinuxScreenReaderBackendCapability ProbeExt(IExtImageCopySupportProbe probe)
    {
        try
        {
            var support = probe.ProbeSupport();
            return support.IsSupported
                ? LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy)
                : LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.ExtImageCopy,
                    support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable, support.ErrorMessage ?? "ext-image-copy-capture-v1 is unavailable.");
        }
        catch (IOException exception)
        {
            return LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.ExtImageCopy, ScreenReadErrorKind.BackendUnavailable, exception.Message);
        }
    }

    public static LinuxScreenBackendDescriptor Wlr(IWlrScreencopySupportProbe? probe = null, Func<WlrScreencopySupportResult, IScreenFrameProvider>? create = null) =>
        new(LinuxScreenReaderBackend.WlrScreencopy,
            () => ProbeWlr(probe ?? throw new InvalidOperationException("Backend probe is not configured.")),
            capability => (create ?? throw new InvalidOperationException("Backend provider is not configured."))(
                capability.IsAvailable ? WlrScreencopySupportResult.Supported() : WlrScreencopySupportResult.Failure(
                    capability.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                    capability.ErrorMessage ?? "wlr-screencopy screen reading backend is unavailable.")));

    private static LinuxScreenReaderBackendCapability ProbeWlr(IWlrScreencopySupportProbe probe)
    {
        try
        {
            var support = probe.ProbeSupport();
            return support.IsSupported
                ? LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy)
                : LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.WlrScreencopy,
                    support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable, support.ErrorMessage ?? "wlr-screencopy screen reading backend is unavailable.");
        }
        catch (IOException exception)
        {
            return LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.WlrScreencopy, ScreenReadErrorKind.BackendUnavailable, exception.Message);
        }
    }

    public static LinuxScreenBackendDescriptor Portal(IPortalScreenCastSupportProbe? probe = null, Func<PortalScreenCastSupportResult, IScreenFrameProvider>? create = null) =>
        new(LinuxScreenReaderBackend.Portal,
            () => ProbePortal(probe ?? throw new InvalidOperationException("Backend probe is not configured.")),
            capability => (create ?? throw new InvalidOperationException("Backend provider is not configured."))(
                capability.IsAvailable ? PortalScreenCastSupportResult.Supported() : PortalScreenCastSupportResult.Failure(
                    capability.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                    capability.ErrorMessage ?? "XDG Desktop Portal ScreenCast is unavailable.")));

    private static LinuxScreenReaderBackendCapability ProbePortal(IPortalScreenCastSupportProbe probe)
    {
        var support = probe.ProbeSupport();
        return support.IsSupported
            ? LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal, support.Diagnostic)
            : LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.Portal,
                support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable, support.ErrorMessage ?? "XDG Desktop Portal ScreenCast is unavailable.", support.Diagnostic);
    }

    public static LinuxScreenBackendDescriptor KWin(IKWinScreenShotSupportProbe? probe = null, Func<KWinScreenShotSupportResult, IScreenFrameProvider>? create = null) =>
        new(LinuxScreenReaderBackend.KWinScreenShot2,
            () => ProbeKWin(probe ?? throw new InvalidOperationException("Backend probe is not configured.")),
            capability => (create ?? throw new InvalidOperationException("Backend provider is not configured."))(
                capability.IsAvailable ? KWinScreenShotSupportResult.Supported() : KWinScreenShotSupportResult.Failure(
                    capability.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                    capability.ErrorMessage ?? "KDE KWin ScreenShot2 is unavailable.")),
            cancellationToken => ProbeKWinAsync(
                probe ?? throw new InvalidOperationException("Backend probe is not configured."),
                cancellationToken));

    private static LinuxScreenReaderBackendCapability ProbeKWin(IKWinScreenShotSupportProbe probe)
    {
        var support = probe.ProbeSupport();
        return support.IsSupported
            ? LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2)
            : LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.KWinScreenShot2,
                support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable, support.ErrorMessage ?? "KDE KWin ScreenShot2 is unavailable.");
    }

    private static async Task<LinuxScreenReaderBackendCapability> ProbeKWinAsync(
        IKWinScreenShotSupportProbe probe,
        CancellationToken cancellationToken)
    {
        var support = await probe.ProbeSupportAsync(cancellationToken).ConfigureAwait(false);
        return support.IsSupported
            ? LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2)
            : LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.KWinScreenShot2,
                support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable, support.ErrorMessage ?? "KDE KWin ScreenShot2 is unavailable.");
    }

    public static LinuxScreenBackendDescriptor Gnome(IGnomeScreenReadingReadiness? readiness = null, Func<GnomeExtensionSupportResult, IScreenFrameProvider>? create = null) =>
        new(LinuxScreenReaderBackend.GnomeExtension,
            () => (readiness ?? throw new InvalidOperationException("GNOME readiness is not configured.")).IsAvailable
                ? LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.GnomeExtension)
                : LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.GnomeExtension, ScreenReadErrorKind.BackendUnavailable,
                    "GNOME Shell extension backend is unavailable or not enabled."),
            capability => (create ?? throw new InvalidOperationException("Backend provider is not configured."))(
                capability.IsAvailable ? GnomeExtensionSupportResult.Supported() : GnomeExtensionSupportResult.Failure(
                    capability.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                    capability.ErrorMessage ?? "GNOME Shell extension screen reading is unavailable.")));

    public static LinuxScreenBackendRegistry FromFactories(
        Func<ExtImageCopySupportResult, IScreenFrameProvider> ext,
        Func<WlrScreencopySupportResult, IScreenFrameProvider> wlr,
        Func<PortalScreenCastSupportResult, IScreenFrameProvider> portal,
        Func<KWinScreenShotSupportResult, IScreenFrameProvider> kwin,
        Func<GnomeExtensionSupportResult, IScreenFrameProvider> gnome)
    {
        ArgumentNullException.ThrowIfNull(ext);
        ArgumentNullException.ThrowIfNull(wlr);
        ArgumentNullException.ThrowIfNull(portal);
        ArgumentNullException.ThrowIfNull(kwin);
        ArgumentNullException.ThrowIfNull(gnome);
        return new LinuxScreenBackendRegistry([Ext(create: ext), Wlr(create: wlr), Portal(create: portal), KWin(create: kwin), Gnome(create: gnome)]);
    }
}
