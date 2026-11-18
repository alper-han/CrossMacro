using CrossMacro.Application.Automation;
using CrossMacro.Application.Profiles;
using CrossMacro.Application.Settings;
using CrossMacro.Core.Models.Automation.Scheduling;
using CrossMacro.Core.Services.Automation;
using CrossMacro.Core.Services.Automation.Scheduling;
using CrossMacro.Core.Services.Automation.Shortcuts;
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

        services.TryAddSingleton<TaskCommandRules<ScheduledTask, ScheduleCommand>>(ScheduleTaskCommandRules.Instance);
        services.TryAddSingleton<TaskCommandRules<ShortcutTask, ShortcutCommand>>(sp => new ShortcutTaskCommandRules(sp.GetRequiredService<IShortcutHotkeyNormalizer>()));
        services.TryAddSingleton<TaskCommandRules<TriggerTask, TriggerCommand>>(TriggerTaskCommandRules.Instance);
        AddRunnableTaskKind<ScheduledTask, ScheduleCommand, IScheduledTaskOperations>(services, ScheduleTaskKind.Instance, static operations => operations.RunTaskAsync);
        AddRunnableTaskKind<ShortcutTask, ShortcutCommand, IShortcutTaskOperations>(services, ShortcutTaskKind.Instance, static operations => operations.RunTaskAsync);
        AddTaskKind<TriggerTask, TriggerCommand>(services, TriggerTaskKind.Instance);
        return services;
    }

    private static void AddTaskKind<TTask, TCommand>(IServiceCollection services, AutomationTaskKind<TTask> kind)
        where TTask : class, new()
        where TCommand : class, ITaskCommand
    {
        services.TryAddSingleton(sp => new ManageAutomationTasks<TTask>(
            kind,
            sp.GetRequiredService<IAutomationTaskStore<TTask>>(),
            sp.GetRequiredService<AutomationTaskMutationGate>()));
        services.TryAddSingleton<IManageAutomationTasks<TTask>>(sp => sp.GetRequiredService<ManageAutomationTasks<TTask>>());
        services.TryAddSingleton<ITaskCommands<TTask, TCommand>>(sp => new TaskCommands<TTask, TCommand>(
            sp.GetRequiredService<IManageAutomationTasks<TTask>>(),
            kind,
            sp.GetRequiredService<TaskCommandRules<TTask, TCommand>>()));
    }

    private static void AddRunnableTaskKind<TTask, TCommand, TOperations>(
        IServiceCollection services,
        AutomationTaskKind<TTask> kind,
        Func<TOperations, Func<Guid, CancellationToken, Task>> run)
        where TTask : class, new()
        where TCommand : class, ITaskCommand
        where TOperations : notnull
    {
        services.TryAddSingleton(sp => new ManageRunnableAutomationTasks<TTask>(
            kind,
            sp.GetRequiredService<IAutomationTaskStore<TTask>>(),
            run(sp.GetRequiredService<TOperations>()),
            sp.GetRequiredService<AutomationTaskMutationGate>()));
        services.TryAddSingleton<IManageAutomationTasks<TTask>>(sp => sp.GetRequiredService<ManageRunnableAutomationTasks<TTask>>());
        services.TryAddSingleton<IManageRunnableAutomationTasks<TTask>>(sp => sp.GetRequiredService<ManageRunnableAutomationTasks<TTask>>());
        services.TryAddSingleton(sp => new RunnableTaskCommands<TTask, TCommand>(
            sp.GetRequiredService<IManageRunnableAutomationTasks<TTask>>(),
            kind,
            sp.GetRequiredService<TaskCommandRules<TTask, TCommand>>()));
        services.TryAddSingleton<ITaskCommands<TTask, TCommand>>(sp => sp.GetRequiredService<RunnableTaskCommands<TTask, TCommand>>());
        services.TryAddSingleton<IRunnableTaskCommands<TTask, TCommand>>(sp => sp.GetRequiredService<RunnableTaskCommands<TTask, TCommand>>());
    }
}
