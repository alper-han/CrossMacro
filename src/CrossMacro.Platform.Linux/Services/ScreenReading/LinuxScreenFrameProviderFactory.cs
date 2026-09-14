
namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class LinuxScreenFrameProviderFactory
{
    private readonly ILinuxScreenReaderCapabilityDetector _capabilityDetector;
    private readonly ILinuxCapabilitySnapshotProvider _snapshotProvider;
    private readonly LinuxScreenBackendRegistry _backends;
    private readonly IX11ScreenCaptureSupportProbe _x11SupportProbe;
    private readonly Func<X11ScreenCaptureSupportResult, IScreenFrameProvider> _x11Factory;

    internal LinuxScreenFrameProviderFactory(
        ILinuxEnvironmentDetector environmentDetector,
        IRuntimeContext runtimeContext,
        ILinuxScreenReaderCapabilityDetector capabilityDetector,
        Func<ExtImageCopySupportResult, IScreenFrameProvider> extFactory,
        Func<WlrScreencopySupportResult, IScreenFrameProvider> wlrFactory,
        Func<PortalScreenCastSupportResult, IScreenFrameProvider> portalFactory,
        Func<KWinScreenShotSupportResult, IScreenFrameProvider> kWinFactory,
        Func<GnomeExtensionSupportResult, IScreenFrameProvider> gnomeFactory,
        IX11ScreenCaptureSupportProbe x11SupportProbe,
        Func<X11ScreenCaptureSupportResult, IScreenFrameProvider> x11Factory)
        : this(environmentDetector, runtimeContext, capabilityDetector, snapshotProvider: null, extFactory, wlrFactory, portalFactory, kWinFactory, gnomeFactory, x11SupportProbe, x11Factory) { /* Empty */ }

    internal LinuxScreenFrameProviderFactory(
        ILinuxEnvironmentDetector environmentDetector,
        IRuntimeContext runtimeContext,
        ILinuxScreenReaderCapabilityDetector capabilityDetector,
        ILinuxCapabilitySnapshotProvider? snapshotProvider,
        Func<ExtImageCopySupportResult, IScreenFrameProvider> extFactory,
        Func<WlrScreencopySupportResult, IScreenFrameProvider> wlrFactory,
        Func<PortalScreenCastSupportResult, IScreenFrameProvider> portalFactory,
        Func<KWinScreenShotSupportResult, IScreenFrameProvider> kWinFactory,
        Func<GnomeExtensionSupportResult, IScreenFrameProvider> gnomeFactory,
        IX11ScreenCaptureSupportProbe x11SupportProbe,
        Func<X11ScreenCaptureSupportResult, IScreenFrameProvider> x11Factory)
    {
        ArgumentNullException.ThrowIfNull(environmentDetector);
        ArgumentNullException.ThrowIfNull(runtimeContext);
        _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
        _snapshotProvider = snapshotProvider ?? new LegacyLinuxScreenSnapshotAdapter(environmentDetector, runtimeContext, capabilityDetector);
        _backends = LinuxScreenBackendDescriptors.FromFactories(extFactory, wlrFactory, portalFactory, kWinFactory, gnomeFactory);
        _x11SupportProbe = x11SupportProbe ?? throw new ArgumentNullException(nameof(x11SupportProbe));
        _x11Factory = x11Factory ?? throw new ArgumentNullException(nameof(x11Factory));
    }

    internal LinuxScreenFrameProviderFactory(
        ILinuxCapabilitySnapshotProvider snapshotProvider,
        ILinuxScreenReaderCapabilityDetector capabilityDetector,
        LinuxScreenBackendRegistry backends,
        IX11ScreenCaptureSupportProbe x11SupportProbe,
        Func<X11ScreenCaptureSupportResult, IScreenFrameProvider> x11Factory)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
        _backends = backends ?? throw new ArgumentNullException(nameof(backends));
        _x11SupportProbe = x11SupportProbe ?? throw new ArgumentNullException(nameof(x11SupportProbe));
        _x11Factory = x11Factory ?? throw new ArgumentNullException(nameof(x11Factory));
    }

    internal LinuxScreenFrameProviderFactory(
        ILinuxEnvironmentDetector environmentDetector,
        IRuntimeContext runtimeContext,
        ILinuxScreenReaderCapabilityDetector capabilityDetector,
        Func<ExtImageCopySupportResult, IScreenFrameProvider> extFactory,
        Func<WlrScreencopySupportResult, IScreenFrameProvider> wlrFactory,
        Func<PortalScreenCastSupportResult, IScreenFrameProvider> portalFactory,
        Func<KWinScreenShotSupportResult, IScreenFrameProvider> kWinFactory,
        IX11ScreenCaptureSupportProbe x11SupportProbe,
        Func<X11ScreenCaptureSupportResult, IScreenFrameProvider> x11Factory)
        : this(
            environmentDetector,
            runtimeContext,
            capabilityDetector,
            extFactory,
            wlrFactory,
            portalFactory,
            kWinFactory,
            static _ => new UnavailableLinuxScreenFrameProvider(ScreenReadErrorKind.BackendUnavailable, "Gnome extension is not configured in tests."),
            x11SupportProbe,
            x11Factory)
    { /* Empty */ }

    public IScreenFrameProvider Create()
    {
        var capabilitySnapshot = _snapshotProvider.GetSnapshot();
        if (capabilitySnapshot.IsWayland)
        {
            return CreateWaylandProvider(capabilitySnapshot);
        }

        if (capabilitySnapshot.IsX11)
        {
            return _x11Factory(_x11SupportProbe.ProbeSupport());
        }

        return new UnavailableLinuxScreenFrameProvider(
            ScreenReadErrorKind.Unsupported,
            $"Linux screen reading is currently supported on Wayland and native X11. Detected compositor: {capabilitySnapshot.Compositor}.");
    }

    private IScreenFrameProvider CreateWaylandProvider(LinuxCapabilitySnapshot centralizedSnapshot)
    {
        var snapshot = centralizedSnapshot.ScreenReading;
        var isFlatpak = centralizedSnapshot.IsFlatpak;
        var compositor = centralizedSnapshot.Compositor;
        var order = LinuxScreenReaderBackendPolicy.GetOrder(isFlatpak, compositor);

        // The synchronous snapshot accessor deliberately does not start external
        // probes. Keep a request-aware provider while discovery is pending; its
        // first capture awaits readiness with the caller's cancellation token.
        if (!_capabilityDetector.IsReady)
        {
            return new LinuxRequestAwareScreenFrameProvider(
                _capabilityDetector,
                order,
                _backends);
        }

        var lastUnavailable = default(LinuxScreenReaderBackendCapability?);
        var permissionDenied = default(LinuxScreenReaderBackendCapability?);

        foreach (var backend in order)
        {
            var capability = snapshot.GetCapability(backend);
            if (capability.IsAvailable)
            {
                return new LinuxRequestAwareScreenFrameProvider(
                    _capabilityDetector,
                    order,
                    _backends);
            }

            lastUnavailable = capability;
            if (permissionDenied is null && capability.ErrorKind is ScreenReadErrorKind.PermissionDenied)
            {
                permissionDenied = capability;
            }
        }

        // GNOME extension discovery is asynchronous. Keep a request-aware provider
        // alive while it is still initializing so the first real capture can wait
        // for the extension instead of permanently falling back to Portal.
        if (_capabilityDetector.IsGnomeSession)
        {
            return new LinuxRequestAwareScreenFrameProvider(
                _capabilityDetector,
                order,
                    _backends);
        }

        var failure = permissionDenied ?? lastUnavailable ?? LinuxScreenReaderBackendCapability.Unavailable(
            LinuxScreenReaderBackend.Portal,
            ScreenReadErrorKind.BackendUnavailable,
            "No Linux Wayland screen reader backend is available.");

        return new UnavailableLinuxScreenFrameProvider(
            failure.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
            BuildUnavailableMessage(snapshot, order, failure));
    }

    private static string BuildUnavailableMessage(
        LinuxScreenReaderCapabilitySnapshot snapshot,
        IReadOnlyList<LinuxScreenReaderBackend> order,
        LinuxScreenReaderBackendCapability failure)
    {
        var attempted = string.Join(", ", order.Select(backend => FormatCapability(snapshot.GetCapability(backend))));
        return $"No usable Linux Wayland screen reader backend is available. Tried {attempted}. Last failure: {failure.ErrorMessage}";
    }

    private static string FormatCapability(LinuxScreenReaderBackendCapability capability) =>
        capability.IsAvailable
            ? $"{capability.Backend}: available"
            : $"{capability.Backend}: {capability.ErrorMessage}";
}
