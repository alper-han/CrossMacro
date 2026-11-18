
namespace CrossMacro.Platform.Linux.Services;

internal sealed class AppImageQuickSetupService(
    LinuxEnvironmentSnapshot environment,
    ILinuxCapabilitySnapshotProvider snapshotProvider,
    LinuxQuickSetupExecutor executor,
    IPrivilegedHostCommandLauncher launcher) : IAppImageQuickSetupService
{
    private readonly ILinuxCapabilitySnapshotProvider _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
    private readonly LinuxQuickSetupExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    private readonly IPrivilegedHostCommandLauncher _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));

    public bool IsApplicable() =>
        OperatingSystem.IsLinux() &&
        !environment.IsFlatpak &&
        !string.IsNullOrWhiteSpace(environment.AppImage) &&
        LinuxDisplaySessionClassifier.IsWayland(environment);

    public bool ShouldPrompt() =>
        IsApplicable() && !_snapshotProvider.GetSnapshot().Input.HasDirectInputAccess;

    public Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default) =>
        _executor.RunAndVerifyAsync(
            _launcher,
            _snapshotProvider,
            "AppImageQuickSetupService",
            "Failed to run quick setup command from AppImage.",
            "The host helper completed, but this app still cannot write /dev/uinput or read usable input devices. Host permissions may have changed; check the container device mapping and access restrictions.",
            cancellationToken);
}
