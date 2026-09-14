namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public interface ILinuxScreenReaderCapabilityDetector
{
    public bool IsGnomeSession { get; }

    /// <summary>
    /// Indicates whether <see cref="GetSnapshot"/> can return acquired capability
    /// data rather than the non-blocking initializing snapshot.
    /// </summary>
    public bool IsReady => true;

    public LinuxScreenReaderCapabilitySnapshot GetSnapshot();

    public Task EnsureReadyAsync(CancellationToken cancellationToken = default);

    public void InvalidateCache();
}
