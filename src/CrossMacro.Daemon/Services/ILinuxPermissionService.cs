namespace CrossMacro.Daemon.Services;

internal interface ILinuxPermissionService
{
    public void ConfigureSocketPermissions(string socketPath);
}
