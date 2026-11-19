namespace CrossMacro.Platform.Linux.Services.Capabilities;

public interface ILinuxCapabilitySnapshotProvider
{
    public LinuxCapabilitySnapshot GetSnapshot();

    /// <summary>Revalidates direct-device access on the next snapshot without resetting other subsystems.</summary>
    public void InvalidateDirectInputCache();

    /// <summary>Re-probes direct-device access from scratch and reports whether it is usable, without probing the daemon.</summary>
    public ValueTask<bool> ProbeDirectInputAccessAsync(CancellationToken cancellationToken = default);

    public void InvalidateScreenReadingCache();

    public void InvalidateCache();
}
