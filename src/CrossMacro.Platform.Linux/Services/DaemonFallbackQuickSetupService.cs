namespace CrossMacro.Platform.Linux.Services;

internal sealed class DaemonFallbackQuickSetupService(
    ILinuxCapabilitySnapshotProvider snapshotProvider,
    LinuxQuickSetupExecutor executor,
    IPrivilegedHostCommandLauncher launcher) : ILinuxDirectInputQuickSetupService
{
    private readonly ILinuxCapabilitySnapshotProvider _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
    private readonly LinuxQuickSetupExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    private readonly IPrivilegedHostCommandLauncher _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));

    public async ValueTask<bool> ShouldPromptAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        var snapshot = _snapshotProvider.GetSnapshot();
        if (snapshot.IsX11 ||
            snapshot.Environment.UsesPortableDirectInput ||
            snapshot.Input.DaemonHandshakeSucceeded)
        {
            return false;
        }

        return !snapshot.Input.CanUseDirectUInput || !snapshot.Input.CanReadInputEvents;
    }

    public async Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var result = await _executor.RunAsync(
            _launcher,
            LinuxQuickSetupScriptOptions.Strict,
            "DaemonFallbackQuickSetupService",
            "Failed to grant temporary direct input access.",
            cancellationToken).ConfigureAwait(false);

        if (result.Success)
        {
            _snapshotProvider.InvalidateCache();
        }

        return result;
    }
}
