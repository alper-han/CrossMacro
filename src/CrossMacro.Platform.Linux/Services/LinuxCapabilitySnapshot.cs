using System;
using System.Threading;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Services.ScreenReading;

namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// One immutable view of the Linux capabilities used by backend selection.
/// Probes are performed by the provider; consumers only inspect this value.
/// </summary>
public readonly record struct LinuxCapabilitySnapshot(
    LinuxEnvironmentSnapshot Environment,
    CompositorType Compositor,
    LinuxInputCapabilitySnapshot Input,
    LinuxScreenReaderCapabilitySnapshot ScreenReading)
{
    public bool IsFlatpak => Environment.IsFlatpak;

    public bool IsWayland => Compositor is not CompositorType.X11 and not CompositorType.Unknown;

    public bool IsX11 => Compositor == CompositorType.X11;

    public bool HasPortalAndPipeWire => ScreenReading.Portal.IsAvailable;

    public bool HasNativeScreenReadingFallback =>
        ScreenReading.ExtImageCopy.IsAvailable ||
        ScreenReading.WlrScreencopy.IsAvailable ||
        ScreenReading.KWinScreenShot2.IsAvailable ||
        ScreenReading.GnomeExtension.IsAvailable;
}

public interface ILinuxCapabilitySnapshotProvider
{
    LinuxCapabilitySnapshot GetSnapshot();

    void InvalidateCache();
}

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
