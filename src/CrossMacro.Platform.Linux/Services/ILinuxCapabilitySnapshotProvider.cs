namespace CrossMacro.Platform.Linux.Services;

public interface ILinuxCapabilitySnapshotProvider
{
    public LinuxCapabilitySnapshot GetSnapshot();

    public void InvalidateCache();
}
