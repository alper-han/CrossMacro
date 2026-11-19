using CrossMacro.Platform.Linux.Services.QuickSetup;

namespace CrossMacro.Platform.Linux.Services;

internal sealed class DaemonFallbackQuickSetupService(
    ILinuxCapabilitySnapshotProvider snapshotProvider,
    LinuxQuickSetupExecutor executor,
    IPrivilegedHostCommandLauncher launcher) : ILinuxDirectInputQuickSetupService
{
    private readonly ILinuxCapabilitySnapshotProvider _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
    private readonly LinuxQuickSetupExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    private readonly IPrivilegedHostCommandLauncher _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));

    public ValueTask<bool> ShouldPromptAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsLinux())
        {
            return ValueTask.FromResult(false);
        }

        var snapshot = _snapshotProvider.GetSnapshot();
        var daemonOrNativeX11Usable = snapshot.IsX11 ||
                                      snapshot.Environment.UsesPortableDirectInput ||
                                      snapshot.Input.DaemonHandshake.Status is LinuxDaemonHandshakeStatus.Success;
        return ValueTask.FromResult(!daemonOrNativeX11Usable && !snapshot.Input.HasDirectInputAccess);
    }

    public Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default) =>
        _executor.RunAndVerifyAsync(
            _launcher,
            _snapshotProvider,
            "DaemonFallbackQuickSetupService",
            "Failed to grant temporary direct input access.",
            "The host helper completed, but this app still cannot write /dev/uinput or read usable input devices. Host permissions may have changed; direct input is not ready.",
            cancellationToken);
}
