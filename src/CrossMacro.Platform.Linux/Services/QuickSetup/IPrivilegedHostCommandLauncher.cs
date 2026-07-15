
namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal interface IPrivilegedHostCommandLauncher
{
    public bool IsAvailable(out string failureMessage);

    public ProcessStartInfo CreateStartInfo(string hostScript, LinuxQuickSetupIdentity identity);
}
