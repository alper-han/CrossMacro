
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class LinuxScreenReaderCapabilityDetectorExtImageCopyTests
{
    [Fact]
    public void WaylandExtImageCopyProbe_WhenRegistryThrowsIOException_ReportsBackendUnavailable()
    {
        var probe = new WaylandExtImageCopySupportProbe(() => throw new IOException("ext transport failed"));

        var result = probe.ProbeSupport();

        Assert.False(result.IsSupported);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Equal("ext transport failed", result.ErrorMessage);
    }

    [Fact]
    public void WlrScreencopyProbe_WhenConnectionThrowsIOException_ReportsBackendUnavailable()
    {
        var probe = new WlrScreencopyCapture(() => throw new IOException("wlr transport failed"));

        var result = probe.ProbeSupport();

        Assert.False(result.IsSupported);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, result.ErrorKind);
        Assert.Equal("wlr transport failed", result.ErrorMessage);
    }

    [Fact]
    public async Task CapabilityDetector_WhenWaylandProbeThrowsIOException_StillEvaluatesRemainingProbes()
    {
        var wlrProbe = new RecordingWlrProbe(WaylandSupportResult.Unsupported("wlr unavailable"));
        var portalProbe = new RecordingPortalProbe(WaylandSupportResult.Unsupported("portal unavailable"));
        var kWinProbe = new RecordingKWinProbe(WaylandSupportResult.Unsupported("not kde"));
        var detector = new LinuxScreenReaderCapabilityDetector(
            new ThrowingExtImageCopyProbe(),
            wlrProbe,
            portalProbe,
            kWinProbe);

        await detector.EnsureReadyAsync(CancellationToken.None);
        var snapshot = detector.GetSnapshot();

        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, snapshot.ExtImageCopy.ErrorKind);
        Assert.Equal("ext probe failed", snapshot.ExtImageCopy.ErrorMessage);
        Assert.Equal(1, wlrProbe.CallCount);
        Assert.Equal(1, portalProbe.CallCount);
        Assert.Equal(1, kWinProbe.CallCount);
    }

    [Fact]
    public async Task ExtImageCopyCapabilityDetector_WhenProbeSupported_ReportsAvailable()
    {
        var detector = new LinuxScreenReaderCapabilityDetector(
            new FakeSupportProbe(WaylandSupportResult.Supported()),
            new FakeSupportProbe(WaylandSupportResult.Unsupported("wlr not implemented")),
            new FakeSupportProbe(WaylandSupportResult.Unsupported("portal unavailable")),
            new FakeSupportProbe(WaylandSupportResult.Unsupported("not kde")));

        await detector.EnsureReadyAsync(CancellationToken.None);
        var snapshot = detector.GetSnapshot();

        Assert.True(snapshot.ExtImageCopy.IsAvailable);
        Assert.Equal(LinuxScreenReaderBackend.ExtImageCopy, snapshot.ExtImageCopy.Backend);
    }

    [Fact]
    public async Task ExtImageCopyCapabilityDetector_WhenProtocolUnsupported_ReportsBackendUnavailable()
    {
        var detector = new LinuxScreenReaderCapabilityDetector(
            new FakeSupportProbe(WaylandSupportResult.Unsupported("ext globals missing")),
            new FakeSupportProbe(WaylandSupportResult.Unsupported("wlr not implemented")),
            new FakeSupportProbe(WaylandSupportResult.Unsupported("portal unavailable")),
            new FakeSupportProbe(WaylandSupportResult.Unsupported("not kde")));

        await detector.EnsureReadyAsync(CancellationToken.None);
        var snapshot = detector.GetSnapshot();

        Assert.False(snapshot.ExtImageCopy.IsAvailable);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, snapshot.ExtImageCopy.ErrorKind);
        Assert.Contains("ext globals missing", snapshot.ExtImageCopy.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CapabilityDetector_WhenInvalidated_ReprobesScreenBackends()
    {
        var probe = new MutableExtImageCopyProbe(WaylandSupportResult.Unsupported("initial"));
        var detector = new LinuxScreenReaderCapabilityDetector(
            probe,
            new FakeSupportProbe(WaylandSupportResult.Unsupported("wlr")),
            new FakeSupportProbe(WaylandSupportResult.Unsupported("portal")),
            new FakeSupportProbe(WaylandSupportResult.Unsupported("kwin")));

        await detector.EnsureReadyAsync(CancellationToken.None);
        Assert.False(detector.GetSnapshot().ExtImageCopy.IsAvailable);
        probe.Result = WaylandSupportResult.Supported();

        detector.InvalidateCache();

        await detector.EnsureReadyAsync(CancellationToken.None);
        Assert.True(detector.GetSnapshot().ExtImageCopy.IsAvailable);
    }

    [Fact]
    public async Task CapabilityDetector_WhenWlrAndPortalProbesReturnMixedResults_MapsBothBackends()
    {
        var detector = new LinuxScreenReaderCapabilityDetector(
            new FakeSupportProbe(WaylandSupportResult.Unsupported("ext globals missing")),
            new FakeSupportProbe(WaylandSupportResult.Supported()),
            new FakeSupportProbe(WaylandSupportResult.Failure(
                ScreenReadErrorKind.PermissionDenied,
                "portal denied")),
            new FakeSupportProbe(WaylandSupportResult.Supported()));

        await detector.EnsureReadyAsync(CancellationToken.None);
        var snapshot = detector.GetSnapshot();

        Assert.True(snapshot.WlrScreencopy.IsAvailable);
        Assert.Equal(LinuxScreenReaderBackend.WlrScreencopy, snapshot.WlrScreencopy.Backend);
        Assert.False(snapshot.Portal.IsAvailable);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, snapshot.Portal.ErrorKind);
        Assert.Contains("portal denied", snapshot.Portal.ErrorMessage, StringComparison.Ordinal);
        Assert.True(snapshot.KWinScreenShot2.IsAvailable);
        Assert.Equal(LinuxScreenReaderBackend.KWinScreenShot2, snapshot.KWinScreenShot2.Backend);
    }

    private sealed class ThrowingExtImageCopyProbe : IExtImageCopySupportProbe
    {
        public WaylandSupportResult ProbeSupport() => throw new IOException("ext probe failed");
    }

    private sealed class MutableExtImageCopyProbe(WaylandSupportResult result) : IExtImageCopySupportProbe
    {
        public WaylandSupportResult Result { get; set; } = result;

        public WaylandSupportResult ProbeSupport() => Result;
    }

    private sealed class RecordingWlrProbe(WaylandSupportResult result) : IWlrScreencopySupportProbe
    {
        public int CallCount { get; private set; }

        public WaylandSupportResult ProbeSupport()
        {
            CallCount++;
            return result;
        }
    }

    private sealed class RecordingPortalProbe(WaylandSupportResult result) : IPortalScreenCastSupportProbe
    {
        public int CallCount { get; private set; }

        public WaylandSupportResult ProbeSupport()
        {
            CallCount++;
            return result;
        }
    }

    private sealed class RecordingKWinProbe(WaylandSupportResult result) : IKWinScreenShotSupportProbe
    {
        public int CallCount { get; private set; }

        public Task<WaylandSupportResult> ProbeSupportAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }
}
