
namespace CrossMacro.Platform.Linux.Services;

internal sealed class FlatpakQuickSetupService(
    LinuxEnvironmentSnapshot environment,
    LinuxQuickSetupExecutor executor,
    IPrivilegedHostCommandLauncher launcher,
    ILinuxCapabilitySnapshotProvider snapshotProvider) : IFlatpakQuickSetupService
{
    private readonly ILinuxCapabilitySnapshotProvider _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
    private readonly LinuxQuickSetupExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    private readonly IPrivilegedHostCommandLauncher _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));

    public bool IsApplicable() =>
        OperatingSystem.IsLinux() &&
        environment.IsFlatpak &&
        LinuxDisplaySessionClassifier.IsWayland(environment);

    public Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default) =>
        _executor.RunAndVerifyAsync(
            _launcher,
            _snapshotProvider,
            "FlatpakQuickSetupService",
            "Failed to run quick setup command inside Flatpak.",
            "The host helper completed, but this app still cannot write /dev/uinput or read usable input devices. Host permissions may have changed; check Flatpak device access restrictions.",
            cancellationToken);
}
