using System.Diagnostics;

namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal interface IPrivilegedHostCommandLauncher
{
    bool IsAvailable(out string failureMessage);

    ProcessStartInfo CreateStartInfo(string hostScript, LinuxQuickSetupIdentity identity);
}
