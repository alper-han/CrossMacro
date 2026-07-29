
namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal interface IPrivilegedHostCommandLauncher
{
    public ValueTask<(bool IsAvailable, string FailureMessage)> IsAvailableAsync(CancellationToken cancellationToken = default);

    public ProcessStartInfo CreateStartInfo(string hostScript, LinuxQuickSetupIdentity identity);
}
