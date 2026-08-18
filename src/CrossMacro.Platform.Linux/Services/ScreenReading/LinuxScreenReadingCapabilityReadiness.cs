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
        if (!_capabilityDetector.IsGnomeSession)
        {
            return Task.CompletedTask;
        }

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
        try
        {
            await _capabilityDetector.EnsureReadyAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _snapshotProvider.InvalidateScreenReadingCache();
        }
    }
}
