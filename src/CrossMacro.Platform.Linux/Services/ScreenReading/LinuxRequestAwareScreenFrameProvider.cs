
namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal sealed class LinuxRequestAwareScreenFrameProvider(
    ILinuxScreenReaderCapabilityDetector capabilityDetector,
    IReadOnlyList<LinuxScreenReaderBackend> order,
    Func<ExtImageCopySupportResult, IScreenFrameProvider> extFactory,
    Func<WlrScreencopySupportResult, IScreenFrameProvider> wlrFactory,
    Func<PortalScreenCastSupportResult, IScreenFrameProvider> portalFactory,
    Func<KWinScreenShotSupportResult, IScreenFrameProvider> kWinFactory,
    Func<GnomeExtensionSupportResult, IScreenFrameProvider> gnomeFactory) : IScreenFrameProvider
{
    private readonly ILinuxScreenReaderCapabilityDetector _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
    private readonly IReadOnlyList<LinuxScreenReaderBackend> _order = order ?? throw new ArgumentNullException(nameof(order));
    private readonly Func<ExtImageCopySupportResult, IScreenFrameProvider> _extFactory = extFactory ?? throw new ArgumentNullException(nameof(extFactory));
    private readonly Func<WlrScreencopySupportResult, IScreenFrameProvider> _wlrFactory = wlrFactory ?? throw new ArgumentNullException(nameof(wlrFactory));
    private readonly Func<PortalScreenCastSupportResult, IScreenFrameProvider> _portalFactory = portalFactory ?? throw new ArgumentNullException(nameof(portalFactory));
    private readonly Func<KWinScreenShotSupportResult, IScreenFrameProvider> _kWinFactory = kWinFactory ?? throw new ArgumentNullException(nameof(kWinFactory));
    private readonly Func<GnomeExtensionSupportResult, IScreenFrameProvider> _gnomeFactory = gnomeFactory ?? throw new ArgumentNullException(nameof(gnomeFactory));
    private readonly Dictionary<LinuxScreenReaderBackend, IScreenFrameProvider> _providers = [];
    private bool _disposed;

    public string ProviderName
    {
        get
        {
            var capability = SelectFirstAvailable();
            return capability is { } selected
                ? GetProvider(selected).ProviderName
                : "Linux screen reader (initializing)";
        }
    }

    public bool IsSupported
    {
        get
        {
            var snapshot = _capabilityDetector.GetSnapshot();
            return _capabilityDetector.IsGnomeSession || _order.Any(backend => snapshot.GetCapability(backend).IsAvailable);
        }
    }

    public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _capabilityDetector.EnsureReadyAsync(options.CancellationToken).ConfigureAwait(false);

        var isFullFrameRequest = region is null;
        var firstIncompatible = default(LinuxScreenReaderBackendCapability?);
        var snapshot = _capabilityDetector.GetSnapshot();

        foreach (var backend in _order)
        {
            var capability = snapshot.GetCapability(backend);
            if (!capability.IsAvailable)
            {
                continue;
            }

            if (!LinuxScreenFrameCaptureModes.SupportsRequest(backend, isFullFrameRequest))
            {
                firstIncompatible ??= capability;
                continue;
            }

            LogSelectedBackend(backend);
            return await GetProvider(capability).CaptureFrameAsync(region, options).ConfigureAwait(false);
        }

        if (firstIncompatible is { } incompatible)
        {
            LogSelectedBackend(incompatible.Backend);
            return await GetProvider(incompatible).CaptureFrameAsync(region, options).ConfigureAwait(false);
        }

        return ScreenReadResultFactory.Failure<ScreenFrame>(
            ScreenReadErrorKind.BackendUnavailable,
            "No Linux Wayland screen reader backend is available.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var provider in _providers.Values)
        {
            provider.Dispose();
        }
    }

    private LinuxScreenReaderBackendCapability? SelectFirstAvailable()
    {
        var snapshot = _capabilityDetector.GetSnapshot();
        foreach (var backend in _order)
        {
            var capability = snapshot.GetCapability(backend);
            if (capability.IsAvailable)
            {
                return capability;
            }
        }

        return null;
    }

    private IScreenFrameProvider GetProvider(LinuxScreenReaderBackendCapability capability)
    {
        if (_providers.TryGetValue(capability.Backend, out var provider))
        {
            return provider;
        }

        provider = LinuxScreenFrameProviderFactory.CreateProvider(
            capability,
            _extFactory,
            _wlrFactory,
            _portalFactory,
            _kWinFactory,
            _gnomeFactory);
        _providers.Add(capability.Backend, provider);
        return provider;
    }

    private static void LogSelectedBackend(LinuxScreenReaderBackend backend) =>
        LoggingExtensions.LogOnce(
            $"LinuxScreenFrameProviderFactory_{backend}",
            "[LinuxScreenFrameProviderFactory] Selected {Backend} screen reader backend",
            backend);
}
