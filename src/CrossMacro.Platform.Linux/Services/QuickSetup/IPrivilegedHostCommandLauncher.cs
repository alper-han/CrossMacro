
namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal interface IPrivilegedHostCommandLauncher
{
    public ValueTask<LinuxQuickSetupIdentity?> ResolveIdentityAsync(LinuxQuickSetupIdentityResolver resolver, CancellationToken cancellationToken = default);

    public ValueTask<(ProcessStartInfo? StartInfo, string FailureMessage)> CreateStartInfoAsync(
        string hostScript, LinuxQuickSetupIdentity identity, CancellationToken cancellationToken = default);
}
