using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Services.ScreenReading;
using CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class LinuxScreenReaderCapabilityDetectorExtImageCopyTests
{
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
}
