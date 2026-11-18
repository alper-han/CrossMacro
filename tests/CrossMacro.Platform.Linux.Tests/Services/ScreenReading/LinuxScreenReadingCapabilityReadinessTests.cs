namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class LinuxScreenReadingCapabilityReadinessTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnsureReadyAsync_AwaitsDetectorAndRefreshesAllBackendCapabilitiesOnce(bool isGnomeSession)
    {
        var detector = Substitute.For<ILinuxScreenReaderCapabilityDetector>();
        _ = detector.IsGnomeSession.Returns(isGnomeSession);
        _ = detector.EnsureReadyAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var snapshotProvider = Substitute.For<ILinuxCapabilitySnapshotProvider>();
        var readiness = new LinuxScreenReadingCapabilityReadiness(detector, snapshotProvider);

        await readiness.EnsureReadyAsync(CancellationToken.None);
        await readiness.EnsureReadyAsync(CancellationToken.None);

        _ = detector.Received(1).EnsureReadyAsync(CancellationToken.None);
        snapshotProvider.Received(1).InvalidateScreenReadingCache();
        snapshotProvider.DidNotReceive().InvalidateCache();
    }
}
