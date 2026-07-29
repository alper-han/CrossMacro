
namespace CrossMacro.UI.Services;

internal sealed class DesktopStartupCoordinator(
    DesktopStartupInitializationService initializationService,
    DesktopPermissionGateService permissionGateService,
    DesktopQuickSetupGateService quickSetupGateService,
    DesktopStartupRuntimeService runtimeService) : IDesktopStartupCoordinator
{
    private readonly DesktopStartupInitializationService _initializationService = initializationService ?? throw new ArgumentNullException(nameof(initializationService));
    private readonly DesktopPermissionGateService _permissionGateService = permissionGateService ?? throw new ArgumentNullException(nameof(permissionGateService));
    private readonly DesktopQuickSetupGateService _quickSetupGateService = quickSetupGateService ?? throw new ArgumentNullException(nameof(quickSetupGateService));
    private readonly DesktopStartupRuntimeService _runtimeService = runtimeService ?? throw new ArgumentNullException(nameof(runtimeService));

    public async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        var startupPreferences = await _initializationService.InitializeAsync().ConfigureAwait(false);
        var permissionGateResult = await _permissionGateService.TryHandleAsync(desktop).ConfigureAwait(false);

        if (permissionGateResult.Handled)
        {
            return;
        }

        var handled = await _quickSetupGateService.TryHandleAsync(
            desktop,
            startupPreferences,
            permissionGateResult.UnsupportedSessionReason,
            (lifetime, preferences) =>
            {
                return _runtimeService.StartAsync(lifetime, preferences);
            }).ConfigureAwait(false);

        if (!handled)
        {
            await _runtimeService.StartAsync(desktop, startupPreferences).ConfigureAwait(false);
        }
    }
}
