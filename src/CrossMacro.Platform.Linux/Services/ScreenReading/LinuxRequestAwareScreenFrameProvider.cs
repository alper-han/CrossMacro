using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Extensions;

namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal sealed class LinuxRequestAwareScreenFrameProvider : IScreenFrameProvider
{
    private readonly LinuxScreenReaderCapabilitySnapshot _snapshot;
    private readonly IReadOnlyList<LinuxScreenReaderBackend> _order;
    private readonly Func<ExtImageCopySupportResult, IScreenFrameProvider> _extFactory;
    private readonly Func<WlrScreencopySupportResult, IScreenFrameProvider> _wlrFactory;
    private readonly Func<PortalScreenCastSupportResult, IScreenFrameProvider> _portalFactory;
    private readonly Func<KWinScreenShotSupportResult, IScreenFrameProvider> _kWinFactory;
    private readonly Dictionary<LinuxScreenReaderBackend, IScreenFrameProvider> _providers = [];
    private bool _disposed;

    public LinuxRequestAwareScreenFrameProvider(
        LinuxScreenReaderCapabilitySnapshot snapshot,
        IReadOnlyList<LinuxScreenReaderBackend> order,
        Func<ExtImageCopySupportResult, IScreenFrameProvider> extFactory,
        Func<WlrScreencopySupportResult, IScreenFrameProvider> wlrFactory,
        Func<PortalScreenCastSupportResult, IScreenFrameProvider> portalFactory,
        Func<KWinScreenShotSupportResult, IScreenFrameProvider> kWinFactory)
    {
        _snapshot = snapshot;
        _order = order ?? throw new ArgumentNullException(nameof(order));
        _extFactory = extFactory ?? throw new ArgumentNullException(nameof(extFactory));
        _wlrFactory = wlrFactory ?? throw new ArgumentNullException(nameof(wlrFactory));
        _portalFactory = portalFactory ?? throw new ArgumentNullException(nameof(portalFactory));
        _kWinFactory = kWinFactory ?? throw new ArgumentNullException(nameof(kWinFactory));
    }

    public string ProviderName => GetProvider(SelectFirstAvailable()).ProviderName;

    public bool IsSupported => _order.Any(backend => _snapshot.GetCapability(backend).IsAvailable);

    public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var isFullFrameRequest = region is null;
        var firstIncompatible = default(LinuxScreenReaderBackendCapability?);

        foreach (var backend in _order)
        {
            var capability = _snapshot.GetCapability(backend);
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
        foreach (var backend in _order)
        {
            var capability = _snapshot.GetCapability(backend);
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
            _kWinFactory);
        _providers.Add(capability.Backend, provider);
        return provider;
    }

    private static void LogSelectedBackend(LinuxScreenReaderBackend backend) =>
        LoggingExtensions.LogOnce(
            $"LinuxScreenFrameProviderFactory_{backend}",
            "[LinuxScreenFrameProviderFactory] Selected {Backend} screen reader backend",
            backend);
}
