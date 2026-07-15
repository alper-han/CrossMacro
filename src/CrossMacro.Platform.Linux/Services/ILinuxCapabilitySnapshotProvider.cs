namespace CrossMacro.Platform.Linux.Services;

public interface ILinuxCapabilitySnapshotProvider
{
    LinuxCapabilitySnapshot GetSnapshot();

    void InvalidateCache();
}
