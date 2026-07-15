
namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class LinuxScreenFrameProviderFactory
{
    private readonly ILinuxEnvironmentDetector _environmentDetector;
    private readonly IRuntimeContext _runtimeContext;
    private readonly ILinuxScreenReaderCapabilityDetector _capabilityDetector;
    private readonly ILinuxCapabilitySnapshotProvider? _snapshotProvider;
    private readonly Func<ExtImageCopySupportResult, IScreenFrameProvider> _extFactory;
    private readonly Func<WlrScreencopySupportResult, IScreenFrameProvider> _wlrFactory;
    private readonly Func<PortalScreenCastSupportResult, IScreenFrameProvider> _portalFactory;
    private readonly Func<KWinScreenShotSupportResult, IScreenFrameProvider> _kWinFactory;
    private readonly Func<GnomeExtensionSupportResult, IScreenFrameProvider> _gnomeFactory;
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
        : this(environmentDetector, runtimeContext, capabilityDetector, snapshotProvider: null, extFactory, wlrFactory, portalFactory, kWinFactory, gnomeFactory, x11SupportProbe, x11Factory, _: true)
    {
    }

    internal LinuxScreenFrameProviderFactory(
        ILinuxEnvironmentDetector environmentDetector,
        IRuntimeContext runtimeContext,
        ILinuxScreenReaderCapabilityDetector capabilityDetector,
        ILinuxCapabilitySnapshotProvider snapshotProvider,
        Func<ExtImageCopySupportResult, IScreenFrameProvider> extFactory,
        Func<WlrScreencopySupportResult, IScreenFrameProvider> wlrFactory,
        Func<PortalScreenCastSupportResult, IScreenFrameProvider> portalFactory,
        Func<KWinScreenShotSupportResult, IScreenFrameProvider> kWinFactory,
        Func<GnomeExtensionSupportResult, IScreenFrameProvider> gnomeFactory,
        IX11ScreenCaptureSupportProbe x11SupportProbe,
        Func<X11ScreenCaptureSupportResult, IScreenFrameProvider> x11Factory)
        : this(environmentDetector, runtimeContext, capabilityDetector, snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider)), extFactory, wlrFactory, portalFactory, kWinFactory, gnomeFactory, x11SupportProbe, x11Factory, _: true)
    {
    }

    private LinuxScreenFrameProviderFactory(
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
        Func<X11ScreenCaptureSupportResult, IScreenFrameProvider> x11Factory,
        bool _)
    {
        _environmentDetector = environmentDetector ?? throw new ArgumentNullException(nameof(environmentDetector));
        _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
        _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
        _snapshotProvider = snapshotProvider;
        _extFactory = extFactory ?? throw new ArgumentNullException(nameof(extFactory));
        _wlrFactory = wlrFactory ?? throw new ArgumentNullException(nameof(wlrFactory));
        _portalFactory = portalFactory ?? throw new ArgumentNullException(nameof(portalFactory));
        _kWinFactory = kWinFactory ?? throw new ArgumentNullException(nameof(kWinFactory));
        _gnomeFactory = gnomeFactory ?? throw new ArgumentNullException(nameof(gnomeFactory));
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
            _ => new UnavailableLinuxScreenFrameProvider(ScreenReadErrorKind.BackendUnavailable, "Gnome extension is not configured in tests."),
            x11SupportProbe,
            x11Factory)
    {
    }

    public IScreenFrameProvider Create()
    {
        var capabilitySnapshot = _snapshotProvider?.GetSnapshot();
        var compositor = capabilitySnapshot?.Compositor ?? _environmentDetector.DetectedCompositor;
        if ((capabilitySnapshot?.IsWayland) is true || (capabilitySnapshot is null && _environmentDetector.IsWayland))
        {
            return CreateWaylandProvider(capabilitySnapshot);
        }

        if ((capabilitySnapshot?.IsX11) is true || (capabilitySnapshot is null && _environmentDetector.IsX11))
        {
            return _x11Factory(_x11SupportProbe.ProbeSupport());
        }

        return new UnavailableLinuxScreenFrameProvider(
            ScreenReadErrorKind.Unsupported,
            $"Linux screen reading is currently supported on Wayland and native X11. Detected compositor: {_environmentDetector.DetectedCompositor}.");
    }

    private IScreenFrameProvider CreateWaylandProvider(LinuxCapabilitySnapshot? centralizedSnapshot = null)
    {
        var snapshot = centralizedSnapshot?.ScreenReading ?? _capabilityDetector.GetSnapshot();
        var isFlatpak = centralizedSnapshot?.IsFlatpak ?? _runtimeContext.IsFlatpak;
        var compositor = centralizedSnapshot?.Compositor ?? _environmentDetector.DetectedCompositor;
        var order = LinuxScreenReaderBackendPolicy.GetOrder(isFlatpak, compositor);
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
                    _extFactory,
                    _wlrFactory,
                    _portalFactory,
                    _kWinFactory,
                    _gnomeFactory);
            }

            lastUnavailable = capability;
            if (permissionDenied is null && capability.ErrorKind is ScreenReadErrorKind.PermissionDenied)
            {
                permissionDenied = capability;
            }
        }

        var failure = permissionDenied ?? lastUnavailable ?? LinuxScreenReaderBackendCapability.Unavailable(
            LinuxScreenReaderBackend.Portal,
            ScreenReadErrorKind.BackendUnavailable,
            "No Linux Wayland screen reader backend is available.");

        return new UnavailableLinuxScreenFrameProvider(
            failure.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
            BuildUnavailableMessage(snapshot, order, failure));
    }

    internal static IScreenFrameProvider CreateProvider(
        LinuxScreenReaderBackendCapability capability,
        Func<ExtImageCopySupportResult, IScreenFrameProvider> extFactory,
        Func<WlrScreencopySupportResult, IScreenFrameProvider> wlrFactory,
        Func<PortalScreenCastSupportResult, IScreenFrameProvider> portalFactory,
        Func<KWinScreenShotSupportResult, IScreenFrameProvider> kWinFactory,
        Func<GnomeExtensionSupportResult, IScreenFrameProvider> gnomeFactory) => capability.Backend switch
    {
        LinuxScreenReaderBackend.KWinScreenShot2 => kWinFactory(ToKWinSupport(capability)),
        LinuxScreenReaderBackend.ExtImageCopy => extFactory(ToExtSupport(capability)),
        LinuxScreenReaderBackend.WlrScreencopy => wlrFactory(ToWlrSupport(capability)),
        LinuxScreenReaderBackend.Portal => portalFactory(ToPortalSupport(capability)),
        LinuxScreenReaderBackend.GnomeExtension => gnomeFactory(ToGnomeSupport(capability)),
        _ => throw new ArgumentOutOfRangeException(nameof(capability), capability.Backend, "Unknown Linux screen reader backend."),
    };

    private static GnomeExtensionSupportResult ToGnomeSupport(LinuxScreenReaderBackendCapability capability) =>
        capability.IsAvailable
            ? GnomeExtensionSupportResult.Supported()
            : GnomeExtensionSupportResult.Failure(
                capability.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                capability.ErrorMessage ?? "GNOME Shell extension screen reading is unavailable.");

    private static ExtImageCopySupportResult ToExtSupport(LinuxScreenReaderBackendCapability capability) =>
        capability.IsAvailable
            ? ExtImageCopySupportResult.Supported()
            : ExtImageCopySupportResult.Failure(
                capability.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                capability.ErrorMessage ?? "ext-image-copy-capture-v1 is unavailable.");

    private static WlrScreencopySupportResult ToWlrSupport(LinuxScreenReaderBackendCapability capability) =>
        capability.IsAvailable
            ? WlrScreencopySupportResult.Supported()
            : WlrScreencopySupportResult.Failure(
                capability.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                capability.ErrorMessage ?? "wlr-screencopy screen reading backend is unavailable.");

    private static PortalScreenCastSupportResult ToPortalSupport(LinuxScreenReaderBackendCapability capability) =>
        capability.IsAvailable
            ? PortalScreenCastSupportResult.Supported()
            : PortalScreenCastSupportResult.Failure(
                capability.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                capability.ErrorMessage ?? "XDG Desktop Portal ScreenCast is unavailable.");

    private static KWinScreenShotSupportResult ToKWinSupport(LinuxScreenReaderBackendCapability capability) =>
        capability.IsAvailable
            ? KWinScreenShotSupportResult.Supported()
            : KWinScreenShotSupportResult.Failure(
                capability.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                capability.ErrorMessage ?? "KDE KWin ScreenShot2 is unavailable.");

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
