using CrossMacro.Platform.Linux.Services.ScreenReading;

namespace CrossMacro.Platform.Linux.Services.Capabilities;

public sealed class LinuxCapabilitySnapshotProvider(
    ILinuxEnvironmentVariables environmentVariables,
    ILinuxInputCapabilitySnapshotProvider inputSnapshotProvider,
    ILinuxScreenReaderCapabilityDetector screenReaderCapabilityDetector) : ILinuxCapabilitySnapshotProvider
{
    private readonly ILinuxEnvironmentVariables _environmentVariables = environmentVariables ?? throw new ArgumentNullException(nameof(environmentVariables));
    private readonly ILinuxInputCapabilitySnapshotProvider _inputSnapshotProvider = inputSnapshotProvider ?? throw new ArgumentNullException(nameof(inputSnapshotProvider));
    private readonly ILinuxScreenReaderCapabilityDetector _screenReaderCapabilityDetector = screenReaderCapabilityDetector ?? throw new ArgumentNullException(nameof(screenReaderCapabilityDetector));

    // Each subsystem owns its own freshness policy. Caching the aggregate would
    // freeze the input provider's TTL and hide asynchronous screen readiness updates.
    public LinuxCapabilitySnapshot GetSnapshot() => CaptureSnapshot();

    public void InvalidateDirectInputCache() => _inputSnapshotProvider.InvalidateDirectInputCache();

    public ValueTask<bool> ProbeDirectInputAccessAsync(CancellationToken cancellationToken = default) =>
        _inputSnapshotProvider.ProbeDirectInputAccessAsync(cancellationToken);

    public void InvalidateScreenReadingCache()
    {
        _screenReaderCapabilityDetector.InvalidateCache();
    }

    public void InvalidateCache()
    {
        _inputSnapshotProvider.InvalidateCache();
        _screenReaderCapabilityDetector.InvalidateCache();
    }

    private LinuxCapabilitySnapshot CaptureSnapshot()
    {
        var environment = _environmentVariables.CaptureSnapshot();
        var compositor = CompositorDetector.ClassifyFromEnvironment(environment, OperatingSystem.IsLinux());
        var screenReading = LinuxDisplaySessionClassifier.IsWayland(environment)
            ? _screenReaderCapabilityDetector.GetSnapshot()
            : LinuxScreenReaderCapabilitySnapshot.NotApplicable(
                "Wayland screen-reading backends are not applicable to the current display session.");
        return new LinuxCapabilitySnapshot(
            environment,
            compositor,
            _inputSnapshotProvider.GetSnapshot(),
            screenReading);
    }
}
