namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class LinuxScreenFrameProviderFactoryReadinessTests
{
    [Fact]
    public async Task Create_WhenGnomeIsStillInitializing_KeepsRequestAwareProviderAndSelectsExtensionAfterReadiness()
    {
        var environmentDetector = Substitute.For<ILinuxEnvironmentDetector>();
        _ = environmentDetector.IsWayland.Returns(returnThis: true);
        _ = environmentDetector.IsX11.Returns(returnThis: false);
        _ = environmentDetector.DetectedCompositor.Returns(returnThis: CompositorType.GNOME);

        var runtimeContext = Substitute.For<IRuntimeContext>();
        _ = runtimeContext.IsFlatpak.Returns(returnThis: false);

        var detector = new DelayedGnomeCapabilityDetector();
        var x11Probe = Substitute.For<IX11ScreenCaptureSupportProbe>();
        var createdProviders = new List<string>();
        var factory = new LinuxScreenFrameProviderFactory(
            environmentDetector,
            runtimeContext,
            detector,
            _ => CreateProvider("ext", createdProviders),
            _ => CreateProvider("wlr", createdProviders),
            _ => CreateProvider("portal", createdProviders),
            _ => CreateProvider("kwin", createdProviders),
            _ => CreateProvider("gnome", createdProviders),
            x11Probe,
            _ => CreateProvider("x11", createdProviders));

        using var provider = factory.Create();
        var result = await provider.CaptureFrameAsync(
            new ScreenRect(0, 0, 1, 1),
            ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, detector.ReadinessCalls);
        Assert.Equal(["gnome"], createdProviders);
    }

    private static IScreenFrameProvider CreateProvider(string name, ICollection<string> createdProviders)
    {
        createdProviders.Add(name);
        return new RecordingScreenFrameProvider(name);
    }

    private sealed class DelayedGnomeCapabilityDetector : ILinuxScreenReaderCapabilityDetector
    {
        private LinuxScreenReaderCapabilitySnapshot _snapshot = CreateSnapshot(gnomeAvailable: false);

        public bool IsGnomeSession => true;

        public int ReadinessCalls { get; private set; }

        public LinuxScreenReaderCapabilitySnapshot GetSnapshot() => _snapshot;

        public Task EnsureReadyAsync(CancellationToken cancellationToken = default)
        {
            ReadinessCalls++;
            _snapshot = CreateSnapshot(gnomeAvailable: true);
            return Task.CompletedTask;
        }

        public void InvalidateCache()
        {
        }

        private static LinuxScreenReaderCapabilitySnapshot CreateSnapshot(bool gnomeAvailable) =>
            new(
                Unavailable(LinuxScreenReaderBackend.KWinScreenShot2),
                Unavailable(LinuxScreenReaderBackend.ExtImageCopy),
                Unavailable(LinuxScreenReaderBackend.WlrScreencopy),
                Unavailable(LinuxScreenReaderBackend.Portal),
                gnomeAvailable
                    ? LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.GnomeExtension)
                    : Unavailable(LinuxScreenReaderBackend.GnomeExtension));

        private static LinuxScreenReaderBackendCapability Unavailable(LinuxScreenReaderBackend backend) =>
            LinuxScreenReaderBackendCapability.Unavailable(backend, ScreenReadErrorKind.BackendUnavailable, "test unavailable");
    }

    private sealed class RecordingScreenFrameProvider(string providerName) : IScreenFrameProvider
    {
        public string ProviderName { get; } = providerName;

        public bool IsSupported => true;

        public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options) =>
            Task.FromResult(ScreenReadResultFactory.Failure<ScreenFrame>(
                ScreenReadErrorKind.CaptureFailed,
                "test provider"));

        public void Dispose()
        {
        }
    }

}
