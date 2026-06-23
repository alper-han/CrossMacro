namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public interface ILinuxScreenReaderCapabilityDetector
{
    LinuxScreenReaderCapabilitySnapshot GetSnapshot();
}
