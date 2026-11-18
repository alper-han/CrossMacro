
namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class LinuxScreenFrameProviderFactory
{
    private readonly ILinuxScreenReaderCapabilityDetector _capabilityDetector;
    private readonly ILinuxCapabilitySnapshotProvider _snapshotProvider;
    private readonly LinuxScreenBackendRegistry _backends;
    private readonly IX11ScreenCaptureSupportProbe _x11SupportProbe;
    private readonly Func<X11ScreenCaptureSupportResult, IScreenFrameProvider> _x11Factory;

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

    private IScreenFrameProvider CreateWaylandProvider(LinuxCapabilitySnapshot capabilitySnapshot)
    {
        var snapshot = capabilitySnapshot.ScreenReading;
        var order = LinuxScreenReaderBackendPolicy.GetOrder(capabilitySnapshot.IsFlatpak, capabilitySnapshot.Compositor);

        // The synchronous snapshot accessor deliberately does not start external probes, so discovery may
        // still be pending, and GNOME extension discovery is asynchronous. In both cases keep a
        // request-aware provider whose first capture awaits readiness with the caller's cancellation token,
        // instead of permanently falling back to Portal.
        if (!_capabilityDetector.IsReady ||
            order.Any(backend => snapshot.GetCapability(backend).IsAvailable) ||
            _capabilityDetector.IsGnomeSession)
        {
            return new LinuxRequestAwareScreenFrameProvider(_capabilityDetector, order, _backends);
        }

        var capabilities = order.Select(snapshot.GetCapability).ToList();
        var failure = SelectFailure(capabilities);

        var attempted = string.Join(", ", capabilities.Select(FormatCapability));
        return new UnavailableLinuxScreenFrameProvider(
            failure.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
            $"No usable Linux Wayland screen reader backend is available. Tried {attempted}. Last failure: {failure.ErrorMessage}");
    }

    private static LinuxScreenReaderBackendCapability SelectFailure(List<LinuxScreenReaderBackendCapability> capabilities)
    {
        if (capabilities.Count is 0)
        {
            return LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.Portal,
                ScreenReadErrorKind.BackendUnavailable,
                "No Linux Wayland screen reader backend is available.");
        }

        var denied = capabilities.FindIndex(static capability => capability.ErrorKind is ScreenReadErrorKind.PermissionDenied);
        return denied >= 0 ? capabilities[denied] : capabilities[^1];
    }

    private static string FormatCapability(LinuxScreenReaderBackendCapability capability) =>
        capability.IsAvailable
            ? $"{capability.Backend}: available"
            : $"{capability.Backend}: {capability.ErrorMessage}";
}
