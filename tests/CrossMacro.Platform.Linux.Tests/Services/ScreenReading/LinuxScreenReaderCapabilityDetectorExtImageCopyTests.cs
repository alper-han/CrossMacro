using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Services.ScreenReading;
using CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

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
    public void CapabilityDetector_WhenWaylandProbeThrowsIOException_StillEvaluatesRemainingProbes()
    {
        var wlrProbe = new RecordingWlrProbe(WlrScreencopySupportResult.Unsupported("wlr unavailable"));
        var portalProbe = new RecordingPortalProbe(PortalScreenCastSupportResult.Unsupported("portal unavailable"));
        var kWinProbe = new RecordingKWinProbe(KWinScreenShotSupportResult.Unsupported("not kde"));
        var detector = new LinuxScreenReaderCapabilityDetector(
            new ThrowingExtImageCopyProbe(),
            wlrProbe,
            portalProbe,
            kWinProbe);

        var snapshot = detector.GetSnapshot();

        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, snapshot.ExtImageCopy.ErrorKind);
        Assert.Equal("ext probe failed", snapshot.ExtImageCopy.ErrorMessage);
        Assert.Equal(1, wlrProbe.CallCount);
        Assert.Equal(1, portalProbe.CallCount);
        Assert.Equal(1, kWinProbe.CallCount);
    }

    [Fact]
    public void ExtImageCopyCapabilityDetector_WhenProbeSupported_ReportsAvailable()
    {
        var detector = new LinuxScreenReaderCapabilityDetector(
            new FakeExtImageCopyProbe(ExtImageCopySupportResult.Supported()),
            new FakeWlrScreencopySupportProbe(WlrScreencopySupportResult.Unsupported("wlr not implemented")),
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Unsupported("portal unavailable")),
            new FakeKWinScreenShotSupportProbe(KWinScreenShotSupportResult.Unsupported("not kde")));

        var snapshot = detector.GetSnapshot();

        Assert.True(snapshot.ExtImageCopy.IsAvailable);
        Assert.Equal(LinuxScreenReaderBackend.ExtImageCopy, snapshot.ExtImageCopy.Backend);
    }

    [Fact]
    public void ExtImageCopyCapabilityDetector_WhenProtocolUnsupported_ReportsBackendUnavailable()
    {
        var detector = new LinuxScreenReaderCapabilityDetector(
            new FakeExtImageCopyProbe(ExtImageCopySupportResult.Unsupported("ext globals missing")),
            new FakeWlrScreencopySupportProbe(WlrScreencopySupportResult.Unsupported("wlr not implemented")),
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Unsupported("portal unavailable")),
            new FakeKWinScreenShotSupportProbe(KWinScreenShotSupportResult.Unsupported("not kde")));

        var snapshot = detector.GetSnapshot();

        Assert.False(snapshot.ExtImageCopy.IsAvailable);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, snapshot.ExtImageCopy.ErrorKind);
        Assert.Contains("ext globals missing", snapshot.ExtImageCopy.ErrorMessage);
    }

    [Fact]
    public void CapabilityDetector_WhenInvalidated_ReprobesScreenBackends()
    {
        var probe = new MutableExtImageCopyProbe(ExtImageCopySupportResult.Unsupported("initial"));
        var detector = new LinuxScreenReaderCapabilityDetector(
            probe,
            new FakeWlrScreencopySupportProbe(WlrScreencopySupportResult.Unsupported("wlr")),
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Unsupported("portal")),
            new FakeKWinScreenShotSupportProbe(KWinScreenShotSupportResult.Unsupported("kwin")));

        Assert.False(detector.GetSnapshot().ExtImageCopy.IsAvailable);
        probe.Result = ExtImageCopySupportResult.Supported();

        detector.InvalidateCache();

        Assert.True(detector.GetSnapshot().ExtImageCopy.IsAvailable);
    }

    [Fact]
    public void CapabilityDetector_WhenWlrAndPortalProbesReturnMixedResults_MapsBothBackends()
    {
        var detector = new LinuxScreenReaderCapabilityDetector(
            new FakeExtImageCopyProbe(ExtImageCopySupportResult.Unsupported("ext globals missing")),
            new FakeWlrScreencopySupportProbe(WlrScreencopySupportResult.Supported()),
            new FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult.Failure(
                ScreenReadErrorKind.PermissionDenied,
                "portal denied")),
            new FakeKWinScreenShotSupportProbe(KWinScreenShotSupportResult.Supported()));

        var snapshot = detector.GetSnapshot();

        Assert.True(snapshot.WlrScreencopy.IsAvailable);
        Assert.Equal(LinuxScreenReaderBackend.WlrScreencopy, snapshot.WlrScreencopy.Backend);
        Assert.False(snapshot.Portal.IsAvailable);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, snapshot.Portal.ErrorKind);
        Assert.Contains("portal denied", snapshot.Portal.ErrorMessage);
        Assert.True(snapshot.KWinScreenShot2.IsAvailable);
        Assert.Equal(LinuxScreenReaderBackend.KWinScreenShot2, snapshot.KWinScreenShot2.Backend);
    }

    private sealed class ThrowingExtImageCopyProbe : IExtImageCopySupportProbe
    {
        public ExtImageCopySupportResult ProbeSupport() => throw new IOException("ext probe failed");
    }

    private sealed class MutableExtImageCopyProbe(ExtImageCopySupportResult result) : IExtImageCopySupportProbe
    {
        public ExtImageCopySupportResult Result { get; set; } = result;

        public ExtImageCopySupportResult ProbeSupport() => Result;
    }

    private sealed class RecordingWlrProbe(WlrScreencopySupportResult result) : IWlrScreencopySupportProbe
    {
        public int CallCount { get; private set; }

        public WlrScreencopySupportResult ProbeSupport()
        {
            CallCount++;
            return result;
        }
    }

    private sealed class RecordingPortalProbe(PortalScreenCastSupportResult result) : IPortalScreenCastSupportProbe
    {
        public int CallCount { get; private set; }

        public PortalScreenCastSupportResult ProbeSupport()
        {
            CallCount++;
            return result;
        }
    }

    private sealed class RecordingKWinProbe(KWinScreenShotSupportResult result) : IKWinScreenShotSupportProbe
    {
        public int CallCount { get; private set; }

        public KWinScreenShotSupportResult ProbeSupport()
        {
            CallCount++;
            return result;
        }
    }
}
