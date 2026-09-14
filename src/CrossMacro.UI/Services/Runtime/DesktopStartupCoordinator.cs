
namespace CrossMacro.UI.Services.Runtime;

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

    public Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop) => StartAsync(desktop, CancellationToken.None);

    public async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        cancellationToken.ThrowIfCancellationRequested();
        var startupPreferences = await _initializationService.InitializeAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var permissionGateResult = await _permissionGateService.TryHandleAsync(desktop, cancellationToken).ConfigureAwait(false);

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
                cancellationToken.ThrowIfCancellationRequested();
                return _runtimeService.StartAsync(lifetime, preferences);
            }, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        if (!handled)
        {
            await _runtimeService.StartAsync(desktop, startupPreferences).ConfigureAwait(false);
        }
    }
}
