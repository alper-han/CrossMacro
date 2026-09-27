namespace CrossMacro.Platform.Linux.Services.Capabilities;

public interface ILinuxCapabilitySnapshotProvider
{
    public LinuxCapabilitySnapshot GetSnapshot();

    /// <summary>Revalidates direct-device access on the next snapshot without resetting other subsystems.</summary>
    public void InvalidateDirectInputCache();

    public void InvalidateScreenReadingCache();

    public void InvalidateCache();
}
