namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

/// <summary>
/// Behaviour every backend provider shares: support reporting, structured failures and backend disposal.
/// Frame mapping that differs per backend stays in the per-backend test classes.
/// </summary>
public sealed class ScreenFrameProviderContractTests
{
    private static readonly ScreenRect Region = new(1, 2, 1, 1);

    private static readonly Dictionary<string, ProviderCase> Cases = new ProviderCase[]
    {
        new ProviderCase(
            "ext-image-copy",
            "Wayland ext-image-copy-capture-v1",
            scenario =>
            {
                var capture = new FakeExtImageCopyCapture(WaylandSupport(scenario.Supported)) { CaptureException = scenario.CaptureException };
                var provider = scenario.ProvideSupport
                    ? new ExtImageCopyScreenFrameProvider(capture, WaylandSupportResult.Supported())
                    : new ExtImageCopyScreenFrameProvider(capture);
                return new ProviderHarness(provider, () => capture.ProbeCalls, () => capture.CaptureCalls, () => capture.DisposeCount);
            }),
        new ProviderCase(
            "wlr-screencopy",
            "Wayland wlr-screencopy-unstable-v1",
            scenario =>
            {
                var capture = new FakeWlrScreencopyCapture(WaylandSupport(scenario.Supported)) { CaptureException = scenario.CaptureException };
                var provider = scenario.ProvideSupport
                    ? new WlrScreencopyScreenFrameProvider(capture, WaylandSupportResult.Supported())
                    : new WlrScreencopyScreenFrameProvider(capture);
                return new ProviderHarness(provider, () => capture.ProbeCalls, () => capture.CaptureCalls, () => capture.DisposeCount);
            }),
        new ProviderCase(
            "kwin-screenshot2",
            "KDE KWin ScreenShot2",
            scenario =>
            {
                var capture = new FakeKWinScreenShotCapture(WaylandSupport(scenario.Supported)) { CaptureException = scenario.CaptureException };
                var provider = scenario.ProvideSupport
                    ? new KWinScreenShotScreenFrameProvider(capture, WaylandSupportResult.Supported())
                    : new KWinScreenShotScreenFrameProvider(capture);
                return new ProviderHarness(
                    provider,
                    () => capture.ProbeCalls,
                    () => capture.CaptureCalls + capture.WorkspaceCaptureCalls,
                    () => capture.DisposeCount);
            }),
        new ProviderCase(
            "portal-screencast",
            "XDG Desktop Portal ScreenCast",
            scenario =>
            {
                var capture = new FakePortalScreenCastCapture(WaylandSupport(scenario.Supported)) { CaptureException = scenario.CaptureException };
                var provider = scenario.ProvideSupport
                    ? new PortalScreenCastScreenFrameProvider(capture, WaylandSupportResult.Supported())
                    : new PortalScreenCastScreenFrameProvider(capture);
                return new ProviderHarness(provider, () => capture.ProbeCalls, () => capture.CaptureCalls, () => capture.DisposeCount);
            }),
        new ProviderCase(
            "x11",
            "X11 XGetImage",
            scenario =>
            {
                var support = scenario.Supported
                    ? X11ScreenCaptureSupportResult.Supported()
                    : X11ScreenCaptureSupportResult.Unsupported(UnsupportedMessage);
                var capture = new FakeX11ScreenCapture(support) { CaptureException = scenario.CaptureException };
                var provider = scenario.ProvideSupport
                    ? new X11ScreenFrameProvider(capture, X11ScreenCaptureSupportResult.Supported())
                    : new X11ScreenFrameProvider(capture);
                return new ProviderHarness(provider, () => capture.ProbeCalls, () => capture.CaptureCalls, () => capture.DisposeCount);
            }),
    }.ToDictionary(static backend => backend.Name, StringComparer.Ordinal);

    private const string UnsupportedMessage = "backend probe says no";

    public static TheoryData<string> Backends => new(Cases.Keys);

    [Theory]
    [MemberData(nameof(Backends))]
    public void WhenProbeIsSupported_ReportsSupportedUnderItsName(string backendName)
    {
        using var harness = Cases[backendName].Create(new ProviderScenario());

        Assert.Equal(Cases[backendName].ProviderName, harness.Provider.ProviderName);
        Assert.True(harness.Provider.IsSupported);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public void WhenSupportIsProvided_DoesNotProbeCaptureBackend(string backendName)
    {
        using var harness = Cases[backendName].Create(new ProviderScenario(Supported: false, ProvideSupport: true));

        Assert.True(harness.Provider.IsSupported);
        Assert.Equal(0, harness.ProbeCalls());
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task WhenBackendIsUnsupported_ReturnsUnavailableAndDoesNotCapture(string backendName)
    {
        using var harness = Cases[backendName].Create(new ProviderScenario(Supported: false));

        var result = await harness.Provider.CaptureFrameAsync(Region, ScreenReadOptions.Default);

        Assert.False(harness.Provider.IsSupported);
        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Contains(UnsupportedMessage, result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, harness.CaptureCalls());
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task WhenCanceledBeforeStart_ReturnsCanceledAndDoesNotCapture(string backendName)
    {
        using var harness = Cases[backendName].Create(new ProviderScenario());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await harness.Provider.CaptureFrameAsync(Region, new ScreenReadOptions(cancellationToken: cancellation.Token));

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(0, harness.CaptureCalls());
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task WhenCaptureThrowsCancellation_ReturnsStructuredCanceledFailure(string backendName)
    {
        using var harness = Cases[backendName].Create(new ProviderScenario(CaptureException: new OperationCanceledException("capture canceled")));

        var result = await harness.Provider.CaptureFrameAsync(Region, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
        Assert.Equal(1, harness.CaptureCalls());
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task WhenCaptureThrowsTimeout_ReturnsStructuredTimeoutFailure(string backendName)
    {
        using var harness = Cases[backendName].Create(new ProviderScenario(CaptureException: new TimeoutException("capture timed out")));

        var result = await harness.Provider.CaptureFrameAsync(Region, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureTimeout, result.ErrorKind);
        Assert.Contains("capture timed out", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(1, harness.CaptureCalls());
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public void WhenDisposed_DisposesCaptureBackendOnce(string backendName)
    {
        var harness = Cases[backendName].Create(new ProviderScenario());

        harness.Dispose();
        harness.Dispose();

        Assert.Equal(1, harness.DisposeCount());
    }

    private static WaylandSupportResult WaylandSupport(bool supported) =>
        supported ? WaylandSupportResult.Supported() : WaylandSupportResult.Unsupported(UnsupportedMessage);

    private sealed record ProviderScenario(bool Supported = true, bool ProvideSupport = false, Exception? CaptureException = null);

    private sealed class ProviderCase(string name, string providerName, Func<ProviderScenario, ProviderHarness> create)
    {
        public string Name { get; } = name;

        public string ProviderName { get; } = providerName;

        public ProviderHarness Create(ProviderScenario scenario) => create(scenario);
    }

    private sealed class ProviderHarness(
        IScreenFrameProvider provider,
        Func<int> probeCalls,
        Func<int> captureCalls,
        Func<int> disposeCount) : IDisposable
    {
        public IScreenFrameProvider Provider { get; } = provider;

        public Func<int> ProbeCalls { get; } = probeCalls;

        public Func<int> CaptureCalls { get; } = captureCalls;

        public Func<int> DisposeCount { get; } = disposeCount;

        public void Dispose() => Provider.Dispose();
    }
}
