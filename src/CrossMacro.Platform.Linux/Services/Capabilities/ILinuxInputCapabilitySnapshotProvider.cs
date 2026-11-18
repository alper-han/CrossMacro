
namespace CrossMacro.Platform.Linux.Services.Capabilities;

/// <summary>
/// The one source of truth for whether the daemon, direct uinput and readable input devices are usable.
/// Results are cached; probes run at most once per freshness window.
/// </summary>
public interface ILinuxInputCapabilitySnapshotProvider
{
    public LinuxInputCapabilitySnapshot GetSnapshot();

    public ValueTask<LinuxInputCapabilitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards the cached uinput/input-device probes and reports whether direct access is usable right now,
    /// without touching the daemon observation.
    /// </summary>
    public ValueTask<bool> ProbeDirectInputAccessAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears direct-device access and mode caches without forgetting daemon history.</summary>
    public void InvalidateDirectInputCache();

    /// <summary>Clears every cached probe result after external setup changed device or daemon availability.</summary>
    public void InvalidateCache();
}
