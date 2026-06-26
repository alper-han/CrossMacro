using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Extensions;

namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal sealed class LinuxRequestAwareScreenFrameProvider : IScreenFrameProvider
{
    private readonly ILinuxScreenReaderCapabilityDetector _capabilityDetector;
    private readonly IReadOnlyList<LinuxScreenReaderBackend> _order;
    private readonly Func<ExtImageCopySupportResult, IScreenFrameProvider> _extFactory;
    private readonly Func<WlrScreencopySupportResult, IScreenFrameProvider> _wlrFactory;
    private readonly Func<PortalScreenCastSupportResult, IScreenFrameProvider> _portalFactory;
    private readonly Func<KWinScreenShotSupportResult, IScreenFrameProvider> _kWinFactory;
    private readonly Func<GnomeExtensionSupportResult, IScreenFrameProvider> _gnomeFactory;
    private readonly Dictionary<LinuxScreenReaderBackend, IScreenFrameProvider> _providers = [];
    private bool _disposed;

    public LinuxRequestAwareScreenFrameProvider(
        ILinuxScreenReaderCapabilityDetector capabilityDetector,
        IReadOnlyList<LinuxScreenReaderBackend> order,
        Func<ExtImageCopySupportResult, IScreenFrameProvider> extFactory,
        Func<WlrScreencopySupportResult, IScreenFrameProvider> wlrFactory,
        Func<PortalScreenCastSupportResult, IScreenFrameProvider> portalFactory,
        Func<KWinScreenShotSupportResult, IScreenFrameProvider> kWinFactory,
        Func<GnomeExtensionSupportResult, IScreenFrameProvider> gnomeFactory)
    {
        _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
        _order = order ?? throw new ArgumentNullException(nameof(order));
        _extFactory = extFactory ?? throw new ArgumentNullException(nameof(extFactory));
        _wlrFactory = wlrFactory ?? throw new ArgumentNullException(nameof(wlrFactory));
        _portalFactory = portalFactory ?? throw new ArgumentNullException(nameof(portalFactory));
        _kWinFactory = kWinFactory ?? throw new ArgumentNullException(nameof(kWinFactory));
        _gnomeFactory = gnomeFactory ?? throw new ArgumentNullException(nameof(gnomeFactory));
    }

    public string ProviderName => GetProvider(SelectFirstAvailable()).ProviderName;

    public bool IsSupported => _order.Any(backend => _capabilityDetector.GetSnapshot().GetCapability(backend).IsAvailable);

    public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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
            return GetProvider(capability).CaptureFrameAsync(region, options);
        }

        if (firstIncompatible is { } incompatible)
        {
            LogSelectedBackend(incompatible.Backend);
            return GetProvider(incompatible).CaptureFrameAsync(region, options);
        }

        return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(
            ScreenReadErrorKind.BackendUnavailable,
            "No Linux Wayland screen reader backend is available."));
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

    private LinuxScreenReaderBackendCapability SelectFirstAvailable()
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

        throw new InvalidOperationException("No Linux Wayland screen reader backend is available.");
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
