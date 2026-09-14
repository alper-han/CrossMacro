
namespace CrossMacro.Platform.Linux.Services.Capabilities;

public interface ILinuxInputCapabilitySnapshotProvider
{
    public LinuxInputCapabilitySnapshot CaptureSnapshot(TimeSpan daemonHandshakeBudget);

    public ValueTask<LinuxInputCapabilitySnapshot> CaptureSnapshotAsync(
        TimeSpan daemonHandshakeBudget,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CaptureSnapshot(daemonHandshakeBudget));
    }
}
