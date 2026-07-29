
namespace CrossMacro.Platform.Linux.Services;

public interface ILinuxInputCapabilitySnapshotProvider
{
    public LinuxInputCapabilitySnapshot CaptureSnapshot(TimeSpan daemonHandshakeBudget);

    public ValueTask<LinuxInputCapabilitySnapshot> CaptureSnapshotAsync(
        TimeSpan daemonHandshakeBudget,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ValueTask.FromResult(CaptureSnapshot(daemonHandshakeBudget));
    }
}
