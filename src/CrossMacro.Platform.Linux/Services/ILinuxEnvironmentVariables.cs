namespace CrossMacro.Platform.Linux.Services;

public interface ILinuxEnvironmentVariables
{
    public LinuxEnvironmentSnapshot CaptureSnapshot();
}
