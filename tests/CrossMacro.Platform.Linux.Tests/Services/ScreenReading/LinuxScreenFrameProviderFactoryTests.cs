using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.DisplayServer.X11;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.ScreenReading;
using CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;
using NSubstitute;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public class LinuxScreenFrameProviderFactoryTests
{
    [Fact]
    public void Create_WhenNativeWaylandAndAllBackendsAvailable_SelectsExtBeforeWlrAndPortal()
    {
        var factory = CreateFactory(
            isFlatpak: false,
            compositor: CompositorType.Other,
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.KWinScreenShot2,
                ScreenReadErrorKind.BackendUnavailable,
                "not kde"),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal));

        using var provider = factory.Create();

        Assert.Equal("ext", provider.ProviderName);
    }

    [Fact]
    public void Create_WhenNativeWaylandAndExtUnavailable_SelectsWlrBeforePortal()
    {
        var factory = CreateFactory(
            isFlatpak: false,
            compositor: CompositorType.Other,
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.KWinScreenShot2,
                ScreenReadErrorKind.BackendUnavailable,
                "not kde"),
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.ExtImageCopy,
                ScreenReadErrorKind.BackendUnavailable,
                "ext unavailable"),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal));

        using var provider = factory.Create();

        Assert.Equal("wlr", provider.ProviderName);
    }

    [Fact]
    public void Create_WhenNativeWaylandAndExtAndWlrUnavailable_SelectsPortalFallback()
    {
        var factory = CreateFactory(
            isFlatpak: false,
            compositor: CompositorType.Other,
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.KWinScreenShot2,
                ScreenReadErrorKind.BackendUnavailable,
                "not kde"),
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.ExtImageCopy,
                ScreenReadErrorKind.BackendUnavailable,
                "ext unavailable"),
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.WlrScreencopy,
                ScreenReadErrorKind.BackendUnavailable,
                "wlr unavailable"),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal));

        using var provider = factory.Create();

        Assert.Equal("portal", provider.ProviderName);
    }

    [Fact]
    public void Create_WhenNativeKdeAndKWinAvailable_SelectsKWinBeforeExtWlrAndPortal()
    {
        var factory = CreateFactory(
            isFlatpak: false,
            compositor: CompositorType.KDE,
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal));

        using var provider = factory.Create();

        Assert.Equal("kwin", provider.ProviderName);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenNativeKdeBoundedRequestAndKWinAndExtAvailable_UsesKWinFirst()
    {
        var kWinProvider = new RecordingScreenFrameProvider(
            "kwin",
            ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.CaptureFailed, "kwin capture failed"));
        var extProvider = new RecordingScreenFrameProvider(
            "ext",
            ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.CaptureFailed, "ext should not capture"));
        var factory = CreateFactoryWithProviders(
            isFlatpak: false,
            compositor: CompositorType.KDE,
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal),
            extProvider: extProvider,
            kWinProvider: kWinProvider);

        using var provider = factory.Create();
        var result = await provider.CaptureFrameAsync(new ScreenRect(1, 2, 3, 4), ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Equal(1, kWinProvider.CaptureCalls);
        Assert.Equal(new ScreenRect(1, 2, 3, 4), kWinProvider.LastRegion);
        Assert.Equal(0, extProvider.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenNativeKdeFullFrameRequestAndKWinAndExtAvailable_SkipsKWinAndUsesExt()
    {
        var kWinCapture = new FakeKWinScreenShotCapture(KWinScreenShotSupportResult.Supported());
        var extProvider = new RecordingScreenFrameProvider(
            "ext",
            ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.CaptureFailed, "ext capture failed"));
        var factory = CreateFactoryWithProviders(
            isFlatpak: false,
            compositor: CompositorType.KDE,
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal),
            extProvider: extProvider,
            kWinFactory: support => new KWinScreenShotScreenFrameProvider(kWinCapture, support));

        using var provider = factory.Create();
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Equal("ext capture failed", result.ErrorMessage);
        Assert.Equal(0, kWinCapture.CaptureCalls);
        Assert.Equal(1, extProvider.CaptureCalls);
        Assert.Null(extProvider.LastRegion);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenNativeKdeFullFrameRequestAndOnlyKWinAvailable_ReturnsKWinUnsupportedWithoutCapture()
    {
        var kWinCapture = new FakeKWinScreenShotCapture(KWinScreenShotSupportResult.Supported());
        var factory = CreateFactoryWithProviders(
            isFlatpak: false,
            compositor: CompositorType.KDE,
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2),
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.ExtImageCopy,
                ScreenReadErrorKind.BackendUnavailable,
                "ext missing"),
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.WlrScreencopy,
                ScreenReadErrorKind.BackendUnavailable,
                "wlr missing"),
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.Portal,
                ScreenReadErrorKind.BackendUnavailable,
                "portal missing"),
            kWinFactory: support => new KWinScreenShotScreenFrameProvider(kWinCapture, support));

        using var provider = factory.Create();
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.Unsupported, result.ErrorKind);
        Assert.Contains("bounded region", result.ErrorMessage);
        Assert.Equal(0, kWinCapture.CaptureCalls);
    }

    [Fact]
    public async Task CaptureFrameAsync_WhenFullFrameCompatibleBackendPermissionDenied_ReturnsDeniedWithoutKWinUnsupportedFallback()
    {
        var kWinCapture = new FakeKWinScreenShotCapture(KWinScreenShotSupportResult.Supported());
        var extProvider = new RecordingScreenFrameProvider(
            "ext",
            ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.PermissionDenied, "ext denied"));
        var factory = CreateFactoryWithProviders(
            isFlatpak: false,
            compositor: CompositorType.KDE,
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal),
            extProvider: extProvider,
            kWinFactory: support => new KWinScreenShotScreenFrameProvider(kWinCapture, support));

        using var provider = factory.Create();
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, result.ErrorKind);
        Assert.Equal("ext denied", result.ErrorMessage);
        Assert.Equal(0, kWinCapture.CaptureCalls);
        Assert.Equal(1, extProvider.CaptureCalls);
    }

    [Fact]
    public void Create_WhenNativeKdeAndKWinPermissionDenied_SelectsExtFallback()
    {
        var factory = CreateFactory(
            isFlatpak: false,
            compositor: CompositorType.KDE,
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.KWinScreenShot2,
                ScreenReadErrorKind.PermissionDenied,
                "kwin denied"),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal));

        using var provider = factory.Create();

        Assert.Equal("ext", provider.ProviderName);
    }

    [Fact]
    public void Create_WhenFlatpakWaylandAndAllBackendsAvailable_SelectsPortalFirst()
    {
        var factory = CreateFactory(
            isFlatpak: true,
            compositor: CompositorType.KDE,
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal));

        using var provider = factory.Create();

        Assert.Equal("portal", provider.ProviderName);
    }

    [Fact]
    public void Create_WhenFlatpakWaylandAndPortalUnavailable_FallsBackToExtThenWlr()
    {
        var factory = CreateFactory(
            isFlatpak: true,
            compositor: CompositorType.KDE,
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.Portal,
                ScreenReadErrorKind.PermissionDenied,
                "portal denied"));

        using var provider = factory.Create();

        Assert.Equal("ext", provider.ProviderName);
    }

    [Fact]
    public async Task Create_WhenNoBackendAvailable_ReturnsStructuredUnavailableProvider()
    {
        var factory = CreateFactory(
            isFlatpak: false,
            compositor: CompositorType.Other,
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.KWinScreenShot2,
                ScreenReadErrorKind.BackendUnavailable,
                "kwin missing"),
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.ExtImageCopy,
                ScreenReadErrorKind.BackendUnavailable,
                "ext missing"),
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.WlrScreencopy,
                ScreenReadErrorKind.BackendUnavailable,
                "wlr missing"),
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.Portal,
                ScreenReadErrorKind.PermissionDenied,
                "portal denied"));

        using var provider = factory.Create();

        Assert.False(provider.IsSupported);
        var unavailable = Assert.IsType<UnavailableLinuxScreenFrameProvider>(provider);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, unavailable.ErrorKind);
        Assert.Contains("portal denied", unavailable.FailureMessage);

        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);
        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, result.ErrorKind);
        Assert.Contains("portal denied", result.ErrorMessage);
    }

    [Fact]
    public async Task Create_WhenNativeKdeKWinPermissionDeniedAndFallbacksUnavailable_PreservesPermissionDenied()
    {
        var factory = CreateFactory(
            isFlatpak: false,
            compositor: CompositorType.KDE,
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.KWinScreenShot2,
                ScreenReadErrorKind.PermissionDenied,
                "kwin denied"),
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.ExtImageCopy,
                ScreenReadErrorKind.BackendUnavailable,
                "ext missing"),
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.WlrScreencopy,
                ScreenReadErrorKind.BackendUnavailable,
                "wlr missing"),
            LinuxScreenReaderBackendCapability.Unavailable(
                LinuxScreenReaderBackend.Portal,
                ScreenReadErrorKind.BackendUnavailable,
                "portal missing"));

        using var provider = factory.Create();

        Assert.False(provider.IsSupported);
        var unavailable = Assert.IsType<UnavailableLinuxScreenFrameProvider>(provider);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, unavailable.ErrorKind);
        Assert.Contains("kwin denied", unavailable.FailureMessage);

        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);
        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, result.ErrorKind);
        Assert.Contains("kwin denied", result.ErrorMessage);
    }

    [Fact]
    public void Create_WhenNativeX11AndCaptureSupported_SelectsX11WithoutProbingWaylandBackends()
    {
        var fixture = ScreenReadingSelectorFixture.X11(X11ScreenCaptureSupportResult.Supported());
        var factory = fixture.CreateFactory();

        using var provider = factory.Create();

        Assert.Equal("x11", provider.ProviderName);
        Assert.Equal(1, fixture.X11ProbeCalls);
        Assert.Equal(1, fixture.X11CreateCount);
        Assert.False(fixture.SnapshotRequested);
        Assert.Equal(0, fixture.ExtCreateCount);
        Assert.Equal(0, fixture.WlrCreateCount);
        Assert.Equal(0, fixture.PortalCreateCount);
        Assert.Equal(0, fixture.KWinCreateCount);
    }

    [Fact]
    public async Task Create_WhenNativeX11CaptureUnsupported_ReturnsUnavailableX11Provider()
    {
        var fixture = ScreenReadingSelectorFixture.X11(X11ScreenCaptureSupportResult.Unsupported("DISPLAY missing"));
        var factory = fixture.CreateFactory();

        using var provider = factory.Create();
        var result = await provider.CaptureFrameAsync(null, ScreenReadOptions.Default);

        Assert.Equal("x11", provider.ProviderName);
        Assert.False(provider.IsSupported);
        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Contains("DISPLAY missing", result.ErrorMessage);
        Assert.False(fixture.SnapshotRequested);
    }

    [Fact]
    public void Create_WhenSessionIsNeitherWaylandNorX11_ReturnsUnsupportedProviderWithoutCreatingBackends()
    {
        var environmentDetector = Substitute.For<ILinuxEnvironmentDetector>();
        environmentDetector.IsWayland.Returns(false);
        environmentDetector.IsX11.Returns(false);
        environmentDetector.DetectedCompositor.Returns(CompositorType.Unknown);

        var runtimeContext = Substitute.For<IRuntimeContext>();
        var capabilityDetector = Substitute.For<ILinuxScreenReaderCapabilityDetector>();
        var x11SupportProbe = Substitute.For<IX11ScreenCaptureSupportProbe>();

        var factory = new LinuxScreenFrameProviderFactory(
            environmentDetector,
            runtimeContext,
            capabilityDetector,
            _ => throw new InvalidOperationException("ext should not be created"),
            _ => throw new InvalidOperationException("wlr should not be created"),
            _ => throw new InvalidOperationException("portal should not be created"),
            _ => throw new InvalidOperationException("kwin should not be created"),
            x11SupportProbe,
            _ => throw new InvalidOperationException("x11 should not be created"));

        using var provider = factory.Create();

        Assert.False(provider.IsSupported);
        var unavailable = Assert.IsType<UnavailableLinuxScreenFrameProvider>(provider);
        Assert.Equal(ScreenReadErrorKind.Unsupported, unavailable.ErrorKind);
        Assert.Contains("Wayland", unavailable.FailureMessage);
        Assert.Contains("X11", unavailable.FailureMessage);
        capabilityDetector.DidNotReceive().GetSnapshot();
        x11SupportProbe.DidNotReceive().ProbeSupport();
    }

    private static LinuxScreenFrameProviderFactory CreateFactory(
        bool isFlatpak,
        CompositorType compositor,
        LinuxScreenReaderBackendCapability kwin,
        LinuxScreenReaderBackendCapability ext,
        LinuxScreenReaderBackendCapability wlr,
        LinuxScreenReaderBackendCapability portal)
    {
        var environmentDetector = Substitute.For<ILinuxEnvironmentDetector>();
        environmentDetector.IsWayland.Returns(true);
        environmentDetector.DetectedCompositor.Returns(compositor);

        var runtimeContext = Substitute.For<IRuntimeContext>();
        runtimeContext.IsFlatpak.Returns(isFlatpak);

        var capabilityDetector = Substitute.For<ILinuxScreenReaderCapabilityDetector>();
        capabilityDetector.GetSnapshot().Returns(new LinuxScreenReaderCapabilitySnapshot(kwin, ext, wlr, portal));
        var x11SupportProbe = Substitute.For<IX11ScreenCaptureSupportProbe>();

        return new LinuxScreenFrameProviderFactory(
            environmentDetector,
            runtimeContext,
            capabilityDetector,
            _ => new NamedScreenFrameProvider("ext"),
            _ => new NamedScreenFrameProvider("wlr"),
            _ => new NamedScreenFrameProvider("portal"),
            _ => new NamedScreenFrameProvider("kwin"),
            x11SupportProbe,
            _ => new NamedScreenFrameProvider("x11"));
    }

    private static LinuxScreenFrameProviderFactory CreateFactoryWithProviders(
        bool isFlatpak,
        CompositorType compositor,
        LinuxScreenReaderBackendCapability kwin,
        LinuxScreenReaderBackendCapability ext,
        LinuxScreenReaderBackendCapability wlr,
        LinuxScreenReaderBackendCapability portal,
        IScreenFrameProvider? extProvider = null,
        IScreenFrameProvider? wlrProvider = null,
        IScreenFrameProvider? portalProvider = null,
        IScreenFrameProvider? kWinProvider = null,
        Func<KWinScreenShotSupportResult, IScreenFrameProvider>? kWinFactory = null)
    {
        var environmentDetector = Substitute.For<ILinuxEnvironmentDetector>();
        environmentDetector.IsWayland.Returns(true);
        environmentDetector.DetectedCompositor.Returns(compositor);

        var runtimeContext = Substitute.For<IRuntimeContext>();
        runtimeContext.IsFlatpak.Returns(isFlatpak);

        var capabilityDetector = Substitute.For<ILinuxScreenReaderCapabilityDetector>();
        capabilityDetector.GetSnapshot().Returns(new LinuxScreenReaderCapabilitySnapshot(kwin, ext, wlr, portal));
        var x11SupportProbe = Substitute.For<IX11ScreenCaptureSupportProbe>();

        return new LinuxScreenFrameProviderFactory(
            environmentDetector,
            runtimeContext,
            capabilityDetector,
            _ => extProvider ?? new NamedScreenFrameProvider("ext"),
            _ => wlrProvider ?? new NamedScreenFrameProvider("wlr"),
            _ => portalProvider ?? new NamedScreenFrameProvider("portal"),
            support => kWinFactory?.Invoke(support) ?? kWinProvider ?? new NamedScreenFrameProvider("kwin"),
            x11SupportProbe,
            _ => new NamedScreenFrameProvider("x11"));
    }

    private sealed class NamedScreenFrameProvider : IScreenFrameProvider
    {
        public NamedScreenFrameProvider(string providerName)
        {
            ProviderName = providerName;
        }

        public string ProviderName { get; }

        public bool IsSupported => true;

        public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(
                ScreenReadErrorKind.CaptureFailed,
                "Test provider does not capture frames."));
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingScreenFrameProvider : IScreenFrameProvider
    {
        private readonly ScreenReadResult<ScreenFrame> _result;

        public RecordingScreenFrameProvider(string providerName, ScreenReadResult<ScreenFrame> result)
        {
            ProviderName = providerName;
            _result = result;
        }

        public string ProviderName { get; }

        public bool IsSupported => true;

        public int CaptureCalls { get; private set; }

        public ScreenRect? LastRegion { get; private set; }

        public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            CaptureCalls++;
            LastRegion = region;
            return Task.FromResult(_result);
        }

        public void Dispose()
        {
        }
    }
}
