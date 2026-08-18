namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class LinuxScreenReadingCapabilityReadinessTests
{
    [Fact]
    public async Task EnsureReadyAsync_AwaitsDetectorAndInvalidatesAggregateSnapshotOnce()
    {
        var detector = Substitute.For<ILinuxScreenReaderCapabilityDetector>();
        _ = detector.IsGnomeSession.Returns(returnThis: true);
        _ = detector.EnsureReadyAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var snapshotProvider = Substitute.For<ILinuxCapabilitySnapshotProvider>();
        var readiness = new LinuxScreenReadingCapabilityReadiness(detector, snapshotProvider);

        await readiness.EnsureReadyAsync(CancellationToken.None);
        await readiness.EnsureReadyAsync(CancellationToken.None);

        _ = detector.Received(1).EnsureReadyAsync(CancellationToken.None);
        snapshotProvider.Received(1).InvalidateScreenReadingCache();
        snapshotProvider.DidNotReceive().InvalidateCache();
    }

    [Fact]
    public async Task EnsureReadyAsync_WhenSessionIsNotGnome_DoesNotProbeOrInvalidate()
    {
        var detector = Substitute.For<ILinuxScreenReaderCapabilityDetector>();
        _ = detector.IsGnomeSession.Returns(returnThis: false);
        var snapshotProvider = Substitute.For<ILinuxCapabilitySnapshotProvider>();
        var readiness = new LinuxScreenReadingCapabilityReadiness(detector, snapshotProvider);

        await readiness.EnsureReadyAsync(CancellationToken.None);

        _ = detector.DidNotReceive().EnsureReadyAsync(Arg.Any<CancellationToken>());
        snapshotProvider.DidNotReceive().InvalidateScreenReadingCache();
        snapshotProvider.DidNotReceive().InvalidateCache();
    }
}
