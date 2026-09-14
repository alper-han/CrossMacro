namespace CrossMacro.Platform.Linux.Services.Runtime;

public interface ILinuxEnvironmentVariables
{
    public LinuxEnvironmentSnapshot CaptureSnapshot();
}
