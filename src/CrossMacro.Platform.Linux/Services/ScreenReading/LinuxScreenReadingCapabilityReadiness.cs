namespace CrossMacro.Platform.Linux.Services.ScreenReading;

/// <summary>
/// Synchronizes Linux screen-reading capability consumers with asynchronous GNOME
/// extension initialization and refreshes the aggregate capability snapshot once.
/// </summary>
public sealed class LinuxScreenReadingCapabilityReadiness(
    ILinuxScreenReaderCapabilityDetector capabilityDetector,
    ILinuxCapabilitySnapshotProvider snapshotProvider) : IScreenReadingCapabilityReadiness
{
    private readonly ILinuxScreenReaderCapabilityDetector _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
    private readonly ILinuxCapabilitySnapshotProvider _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
    private readonly Lock _lock = new();
    private Task? _readinessTask;

    public Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        Task readinessTask;
        lock (_lock)
        {
            _readinessTask ??= EnsureReadyCoreAsync();
            readinessTask = _readinessTask;
        }

        return readinessTask.WaitAsync(cancellationToken);
    }

    private async Task EnsureReadyCoreAsync()
    {
        // Start each readiness cycle from a fresh cache. The detector itself owns
        // the asynchronous acquisition and publishes the resulting snapshot only
        // after every backend has completed.
        _snapshotProvider.InvalidateScreenReadingCache();
        await _capabilityDetector.EnsureReadyAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
