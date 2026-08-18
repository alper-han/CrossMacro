
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
    private readonly Lock _providerGate = new();
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly Lock _captureAdmissionGate = new();
    private TaskCompletionSource _captureAdmissionsDrained = CreateCompletedSource();
    private LinuxScreenReaderBackend? _activeBackend;
    private int _captureAdmissions;
    private int _disposeState;

    public string ProviderName
    {
        get
        {
            if (Volatile.Read(ref _disposeState) is not 0)
            {
                return "Linux screen reader (disposed)";
            }

            if (_activeBackend is { } active)
            {
                var activeCapability = GetCapability(active);
                if (activeCapability.IsAvailable)
                {
                    return GetProviderName(activeCapability);
                }
            }

            var capability = SelectFirstAvailable();
            return capability is { } selected ? GetProviderName(selected) : "Linux screen reader (initializing)";
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
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) is not 0, this);

        var admitted = false;
        try
        {
            EnterCapture();
            admitted = true;
            using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                options.CancellationToken,
                _disposeCancellation.Token);
            var operationOptions = new ScreenReadOptions(
                options.Timeout,
                options.PollInterval,
                options.PollUntilMatch,
                operationCancellation.Token);
            await _captureGate.WaitAsync(operationOptions.CancellationToken).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) is not 0, this);
                await _capabilityDetector.EnsureReadyAsync(operationOptions.CancellationToken).ConfigureAwait(false);
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) is not 0, this);
                return await CaptureSerializedAsync(region, operationOptions).ConfigureAwait(false);
            }
            finally
            {
                _ = _captureGate.Release();
            }
        }
        finally
        {
            if (admitted)
            {
                ExitCapture();
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) is not 0)
        {
            return;
        }

        _disposeCancellation.Cancel();
        Task drained;
        lock (_captureAdmissionGate)
        {
            drained = _captureAdmissionsDrained.Task;
        }

        drained.GetAwaiter().GetResult();
        try
        {
            IScreenFrameProvider[] providers;
            lock (_providerGate)
            {
                providers = [.. _providers.Values];
                _providers.Clear();
            }

            foreach (var provider in providers)
            {
                provider.Dispose();
            }
        }
        finally
        {
            _disposeCancellation.Dispose();
        }
    }

    private void EnterCapture()
    {
        lock (_captureAdmissionGate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) is not 0, this);
            if (_captureAdmissions++ is 0)
            {
                _captureAdmissionsDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    private void ExitCapture()
    {
        lock (_captureAdmissionGate)
        {
            if (--_captureAdmissions is 0)
            {
                _ = _captureAdmissionsDrained.TrySetResult();
            }
        }
    }

    private static TaskCompletionSource CreateCompletedSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = source.TrySetResult();
        return source;
    }

    private async Task<ScreenReadResult<ScreenFrame>> CaptureSerializedAsync(ScreenRect? region, ScreenReadOptions options)
    {
        var isFullFrameRequest = region is null;
        var snapshot = _capabilityDetector.GetSnapshot();
        var attempted = new HashSet<LinuxScreenReaderBackend>();
        LinuxScreenReaderBackendCapability? firstIncompatible = null;
        ScreenReadResult<ScreenFrame>? lastFallbackFailure = null;

        if (_activeBackend is { } active)
        {
            var activeCapability = snapshot.GetCapability(active);
            if (activeCapability.IsAvailable && LinuxScreenFrameCaptureModes.SupportsRequest(active, isFullFrameRequest))
            {
                attempted.Add(active);
                var activeResult = await CaptureWithBackendAsync(activeCapability, region, options).ConfigureAwait(false);
                if (activeResult.IsSuccess || !IsFallbackEligible(activeResult.ErrorKind))
                {
                    return activeResult;
                }

                lastFallbackFailure = activeResult;
                _activeBackend = null;
            }
            else
            {
                _activeBackend = null;
            }
        }

        foreach (var backend in _order)
        {
            if (!attempted.Add(backend))
            {
                continue;
            }

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

            var result = await CaptureWithBackendAsync(capability, region, options).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                _activeBackend = backend;
                return result;
            }

            if (!IsFallbackEligible(result.ErrorKind))
            {
                return result;
            }

            lastFallbackFailure = result;
        }

        if (firstIncompatible is { } incompatible)
        {
            var result = await CaptureWithBackendAsync(incompatible, region, options).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                _activeBackend = incompatible.Backend;
            }

            return result;
        }

        return lastFallbackFailure ?? ScreenReadResultFactory.Failure<ScreenFrame>(
            ScreenReadErrorKind.BackendUnavailable,
            "No Linux Wayland screen reader backend is available.");
    }

    private async Task<ScreenReadResult<ScreenFrame>> CaptureWithBackendAsync(
        LinuxScreenReaderBackendCapability capability,
        ScreenRect? region,
        ScreenReadOptions options)
    {
        LogSelectedBackend(capability.Backend);
        return await GetProvider(capability).CaptureFrameAsync(region, options).ConfigureAwait(false);
    }

    private LinuxScreenReaderBackendCapability GetCapability(LinuxScreenReaderBackend backend) =>
        _capabilityDetector.GetSnapshot().GetCapability(backend);

    private static bool IsFallbackEligible(ScreenReadErrorKind? errorKind) =>
        errorKind is ScreenReadErrorKind.BackendUnavailable or ScreenReadErrorKind.Unsupported;

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

    private string GetProviderName(LinuxScreenReaderBackendCapability capability)
    {
        lock (_providerGate)
        {
            return Volatile.Read(ref _disposeState) is 0
                ? GetProviderCore(capability).ProviderName
                : "Linux screen reader (disposed)";
        }
    }

    private IScreenFrameProvider GetProvider(LinuxScreenReaderBackendCapability capability)
    {
        lock (_providerGate)
        {
            return GetProviderCore(capability);
        }
    }

    private IScreenFrameProvider GetProviderCore(LinuxScreenReaderBackendCapability capability)
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
