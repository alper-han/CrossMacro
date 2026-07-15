
namespace CrossMacro.Platform.Linux.Services;

public interface ILinuxInputCapabilitySnapshotProvider
{
    public LinuxInputCapabilitySnapshot CaptureSnapshot(TimeSpan daemonHandshakeBudget);
}
