namespace CrossMacro.Platform.Linux.Services;

public interface ILinuxEnvironmentVariables
{
    LinuxEnvironmentSnapshot CaptureSnapshot();
}
