namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public interface ILinuxScreenReaderCapabilityDetector
{
    public LinuxScreenReaderCapabilitySnapshot GetSnapshot();

    public void InvalidateCache();
}
