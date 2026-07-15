using System;
using System.Threading;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.Services.ScreenReading;

namespace CrossMacro.Platform.Linux.Services;

public sealed class LinuxCapabilitySnapshotProvider : ILinuxCapabilitySnapshotProvider
{
    private readonly ILinuxEnvironmentVariables _environmentVariables;
    private readonly ILinuxInputCapabilityDetector _inputCapabilityDetector;
    private readonly ILinuxScreenReaderCapabilityDetector _screenReaderCapabilityDetector;
    private Lazy<LinuxCapabilitySnapshot> _snapshot;

    public LinuxCapabilitySnapshotProvider(
        ILinuxEnvironmentVariables environmentVariables,
        ILinuxInputCapabilityDetector inputCapabilityDetector,
        ILinuxScreenReaderCapabilityDetector screenReaderCapabilityDetector)
    {
        _environmentVariables = environmentVariables ?? throw new ArgumentNullException(nameof(environmentVariables));
        _inputCapabilityDetector = inputCapabilityDetector ?? throw new ArgumentNullException(nameof(inputCapabilityDetector));
        _screenReaderCapabilityDetector = screenReaderCapabilityDetector ?? throw new ArgumentNullException(nameof(screenReaderCapabilityDetector));
        _snapshot = new Lazy<LinuxCapabilitySnapshot>(CaptureSnapshot, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public LinuxCapabilitySnapshot GetSnapshot() => _snapshot.Value;

    public void InvalidateCache()
    {
        _inputCapabilityDetector.InvalidateCache();
        _screenReaderCapabilityDetector.InvalidateCache();
        _snapshot = new Lazy<LinuxCapabilitySnapshot>(CaptureSnapshot, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private LinuxCapabilitySnapshot CaptureSnapshot()
    {
        var environment = _environmentVariables.CaptureSnapshot();
        var compositor = CompositorDetector.ClassifyFromEnvironment(environment, OperatingSystem.IsLinux());
        return new LinuxCapabilitySnapshot(
            environment,
            compositor,
            _inputCapabilityDetector.GetSnapshot(),
            _screenReaderCapabilityDetector.GetSnapshot());
    }
}
