namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public interface ILinuxScreenReaderCapabilityDetector
{
    public bool IsGnomeSession { get; }

    public LinuxScreenReaderCapabilitySnapshot GetSnapshot();

    public Task EnsureReadyAsync(CancellationToken cancellationToken = default);

    public void InvalidateCache();
}
