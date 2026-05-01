using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CrossMacro.UI.Startup;

namespace CrossMacro.UI.Services;

internal sealed class DesktopStartupCoordinator : IDesktopStartupCoordinator
{
    private readonly DesktopStartupInitializationService _initializationService;
    private readonly DesktopPermissionGateService _permissionGateService;
    private readonly DesktopQuickSetupGateService _quickSetupGateService;
    private readonly DesktopStartupRuntimeService _runtimeService;

    public DesktopStartupCoordinator(
        DesktopStartupInitializationService initializationService,
        DesktopPermissionGateService permissionGateService,
        DesktopQuickSetupGateService quickSetupGateService,
        DesktopStartupRuntimeService runtimeService)
    {
        _initializationService = initializationService ?? throw new ArgumentNullException(nameof(initializationService));
        _permissionGateService = permissionGateService ?? throw new ArgumentNullException(nameof(permissionGateService));
        _quickSetupGateService = quickSetupGateService ?? throw new ArgumentNullException(nameof(quickSetupGateService));
        _runtimeService = runtimeService ?? throw new ArgumentNullException(nameof(runtimeService));
    }

    public async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        var startupPreferences = _initializationService.Initialize();
        var permissionGateResult = await _permissionGateService.TryHandleAsync(desktop);

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
                _runtimeService.Start(lifetime, preferences);
                return Task.CompletedTask;
            });

        if (!handled)
        {
            _runtimeService.Start(desktop, startupPreferences);
        }
    }
}
