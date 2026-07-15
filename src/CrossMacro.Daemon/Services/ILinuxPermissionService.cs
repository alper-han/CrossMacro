namespace CrossMacro.Daemon.Services;

public interface ILinuxPermissionService
{
    public void ConfigureSocketPermissions(string socketPath);
}
