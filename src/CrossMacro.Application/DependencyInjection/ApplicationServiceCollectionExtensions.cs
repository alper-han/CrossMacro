using CrossMacro.Application.Automation;
using CrossMacro.Application.Profiles;
using CrossMacro.Application.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrossMacro.Application.DependencyInjection;

/// <summary>Registers application policies once across the GUI, CLI, and MCP entry points.</summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddCrossMacroApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<AutomationTaskAuthorization>();
        services.TryAddSingleton<AutomationTaskMutationGate>();
        services.TryAddSingleton<SettingsChangeCoordinator>();
        services.TryAddSingleton<IManageProfile, ManageProfile>();
        services.TryAddSingleton<IProfileOperations, ProfileOperations>();
        services.TryAddSingleton<IManageTextExpansion, ManageTextExpansion>();
        services.TryAddSingleton<IManageSchedule, ManageSchedule>();
        services.TryAddSingleton<IManageShortcut, ManageShortcut>();
        services.TryAddSingleton<IManageTrigger, ManageTrigger>();
        services.TryAddSingleton<IScheduleCommands, ScheduleCommands>();
        services.TryAddSingleton<IShortcutCommands, ShortcutCommands>();
        services.TryAddSingleton<ITriggerCommands, TriggerCommands>();
        return services;
    }
}
