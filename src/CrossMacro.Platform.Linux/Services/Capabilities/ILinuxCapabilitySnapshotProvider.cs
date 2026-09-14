namespace CrossMacro.Platform.Linux.Services.Capabilities;

public interface ILinuxCapabilitySnapshotProvider
{
    public LinuxCapabilitySnapshot GetSnapshot();

    public void InvalidateScreenReadingCache();

    public void InvalidateCache();
}
