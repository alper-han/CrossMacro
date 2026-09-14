
namespace CrossMacro.Platform.Linux.Services.Capabilities;

public sealed class LinuxCapabilitySnapshotProvider(
    ILinuxEnvironmentVariables environmentVariables,
    ILinuxInputCapabilityDetector inputCapabilityDetector,
    ILinuxScreenReaderCapabilityDetector screenReaderCapabilityDetector) : ILinuxCapabilitySnapshotProvider
{
    private readonly ILinuxEnvironmentVariables _environmentVariables = environmentVariables ?? throw new ArgumentNullException(nameof(environmentVariables));
    private readonly ILinuxInputCapabilityDetector _inputCapabilityDetector = inputCapabilityDetector ?? throw new ArgumentNullException(nameof(inputCapabilityDetector));
    private readonly ILinuxScreenReaderCapabilityDetector _screenReaderCapabilityDetector = screenReaderCapabilityDetector ?? throw new ArgumentNullException(nameof(screenReaderCapabilityDetector));

    // Each subsystem owns its own freshness policy. Caching the aggregate would
    // freeze the input detector's TTL and hide asynchronous screen readiness updates.
    public LinuxCapabilitySnapshot GetSnapshot() => CaptureSnapshot();

    public void InvalidateScreenReadingCache()
    {
        _screenReaderCapabilityDetector.InvalidateCache();
    }

    public void InvalidateCache()
    {
        _inputCapabilityDetector.InvalidateCache();
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
            _inputCapabilityDetector.GetSnapshot(),
            screenReading);
    }
}
