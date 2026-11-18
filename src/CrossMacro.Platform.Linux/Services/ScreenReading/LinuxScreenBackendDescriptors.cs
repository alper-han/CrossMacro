namespace CrossMacro.Platform.Linux.Services.ScreenReading;

/// <summary>
/// One descriptor per screen backend. A descriptor is built with a probe, a provider factory, or both:
/// capability detection needs only the probe and frame capture needs only the factory, so each side
/// reports a missing half instead of requiring a placeholder for it.
/// </summary>
internal static class LinuxScreenBackendDescriptors
{
    public static LinuxScreenBackendDescriptor Ext(IExtImageCopySupportProbe? probe = null, Func<WaylandSupportResult, IScreenFrameProvider>? create = null) =>
        Describe(
            LinuxScreenReaderBackend.ExtImageCopy,
            "ext-image-copy-capture-v1 is unavailable.",
            probe,
            static p => p.ProbeSupport(),
            static support => (support.IsSupported, support.ErrorKind, support.ErrorMessage, Diagnostic: null),
            create,
            static () => WaylandSupportResult.Supported(),
            static (kind, message) => WaylandSupportResult.Failure(kind, message));

    public static LinuxScreenBackendDescriptor Wlr(IWlrScreencopySupportProbe? probe = null, Func<WaylandSupportResult, IScreenFrameProvider>? create = null) =>
        Describe(
            LinuxScreenReaderBackend.WlrScreencopy,
            "wlr-screencopy screen reading backend is unavailable.",
            probe,
            static p => p.ProbeSupport(),
            static support => (support.IsSupported, support.ErrorKind, support.ErrorMessage, Diagnostic: null),
            create,
            static () => WaylandSupportResult.Supported(),
            static (kind, message) => WaylandSupportResult.Failure(kind, message));

    public static LinuxScreenBackendDescriptor Portal(IPortalScreenCastSupportProbe? probe = null, Func<WaylandSupportResult, IScreenFrameProvider>? create = null) =>
        Describe(
            LinuxScreenReaderBackend.Portal,
            "XDG Desktop Portal ScreenCast is unavailable.",
            probe,
            static p => p.ProbeSupport(),
            static support => (support.IsSupported, support.ErrorKind, support.ErrorMessage, support.Diagnostic),
            create,
            static () => WaylandSupportResult.Supported(),
            static (kind, message) => WaylandSupportResult.Failure(kind, message));

    public static LinuxScreenBackendDescriptor KWin(IKWinScreenShotSupportProbe? probe = null, Func<WaylandSupportResult, IScreenFrameProvider>? create = null) =>
        Describe(
            LinuxScreenReaderBackend.KWinScreenShot2,
            "KDE KWin ScreenShot2 is unavailable.",
            probe,
            static p => p.ProbeSupport(),
            static support => (support.IsSupported, support.ErrorKind, support.ErrorMessage, Diagnostic: null),
            create,
            static () => WaylandSupportResult.Supported(),
            static (kind, message) => WaylandSupportResult.Failure(kind, message),
            static (p, cancellationToken) => p.ProbeSupportAsync(cancellationToken));

    public static LinuxScreenBackendDescriptor Gnome(IGnomeScreenReadingReadiness? readiness = null, Func<GnomeExtensionSupportResult, IScreenFrameProvider>? create = null) =>
        Describe(
            LinuxScreenReaderBackend.GnomeExtension,
            "GNOME Shell extension screen reading is unavailable.",
            readiness,
            static r => r.IsAvailable,
            static isAvailable => (isAvailable, ScreenReadErrorKind.BackendUnavailable, "GNOME Shell extension backend is unavailable or not enabled.", Diagnostic: null),
            create,
            GnomeExtensionSupportResult.Supported,
            GnomeExtensionSupportResult.Failure);

    public static LinuxScreenBackendRegistry FromFactories(
        Func<WaylandSupportResult, IScreenFrameProvider> ext,
        Func<WaylandSupportResult, IScreenFrameProvider> wlr,
        Func<WaylandSupportResult, IScreenFrameProvider> portal,
        Func<WaylandSupportResult, IScreenFrameProvider> kwin,
        Func<GnomeExtensionSupportResult, IScreenFrameProvider> gnome)
    {
        ArgumentNullException.ThrowIfNull(ext);
        ArgumentNullException.ThrowIfNull(wlr);
        ArgumentNullException.ThrowIfNull(portal);
        ArgumentNullException.ThrowIfNull(kwin);
        ArgumentNullException.ThrowIfNull(gnome);
        return new LinuxScreenBackendRegistry([Ext(create: ext), Wlr(create: wlr), Portal(create: portal), KWin(create: kwin), Gnome(create: gnome)]);
    }

    private static LinuxScreenBackendDescriptor Describe<TProbe, TProbeResult, TSupport>(
        LinuxScreenReaderBackend backend,
        string unavailableMessage,
        TProbe? probe,
        Func<TProbe, TProbeResult> probeSupport,
        Func<TProbeResult, (bool IsSupported, ScreenReadErrorKind? ErrorKind, string? ErrorMessage, string? Diagnostic)> describeSupport,
        Func<TSupport, IScreenFrameProvider>? create,
        Func<TSupport> supported,
        Func<ScreenReadErrorKind, string, TSupport> failed,
        Func<TProbe, CancellationToken, Task<TProbeResult>>? probeSupportAsync = null)
        where TProbe : class
    {
        TProbe RequireProbe() => probe ?? throw new InvalidOperationException($"No {backend} probe was supplied.");

        LinuxScreenReaderBackendCapability ToCapability(TProbeResult result)
        {
            var (isSupported, errorKind, errorMessage, diagnostic) = describeSupport(result);
            return isSupported
                ? LinuxScreenReaderBackendCapability.Available(backend, diagnostic)
                : LinuxScreenReaderBackendCapability.Unavailable(
                    backend,
                    errorKind ?? ScreenReadErrorKind.BackendUnavailable,
                    errorMessage ?? unavailableMessage,
                    diagnostic);
        }

        return new LinuxScreenBackendDescriptor(
            backend,
            () =>
            {
                try
                {
                    return ToCapability(probeSupport(RequireProbe()));
                }
                catch (IOException exception)
                {
                    return LinuxScreenReaderBackendCapability.Unavailable(backend, ScreenReadErrorKind.BackendUnavailable, exception.Message);
                }
            },
            capability => (create ?? throw new InvalidOperationException($"No {backend} provider factory was supplied."))(
                capability.IsAvailable
                    ? supported()
                    : failed(capability.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable, capability.ErrorMessage ?? unavailableMessage)),
            probeSupportAsync is null
                ? null
                : async cancellationToken => ToCapability(await probeSupportAsync(RequireProbe(), cancellationToken).ConfigureAwait(false)));
    }
}
