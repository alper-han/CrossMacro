using CrossMacro.Application.DependencyInjection;
using CrossMacro.Core.Services.Automation;
using CrossMacro.Editing.Services;
using CrossMacro.Infrastructure.Services.Automation.Scheduling;
using CrossMacro.Infrastructure.Services.Automation.Shortcuts;
using CrossMacro.Infrastructure.Services.Automation.Triggers;
using CrossMacro.Infrastructure.Services.Editing;
using CrossMacro.Infrastructure.Services.Input;
using CrossMacro.Infrastructure.Services.Profiles;

namespace CrossMacro.Infrastructure.DependencyInjection;

/// <summary>The three automation services (schedules, shortcuts, triggers) and the editor helpers.</summary>
internal static class WorkflowRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddCrossMacroApplicationServices();

        _ = services.AddSingleton<IScheduledTaskRepository>(sp => new JsonScheduledTaskRepository(sp.GetRequiredService<ApplicationPaths>().GetConfigFilePath(ConfigFileNames.Schedules)));
        _ = services.AddSingleton<IScheduledTaskExecutor, MacroScheduledTaskExecutor>();
        _ = services.AddSingleton<SchedulerService>();
        _ = services.AddSingleton<ISchedulerService>(sp => sp.GetRequiredService<SchedulerService>());
        _ = services.AddSingleton<IScheduledTaskOperations>(sp => sp.GetRequiredService<SchedulerService>());
        _ = services.AddSingleton<IAutomationTaskStore<ScheduledTask>>(sp => sp.GetRequiredService<SchedulerService>());

        _ = services.AddSingleton<IShortcutTaskRepository>(sp => new JsonShortcutTaskRepository(
            sp.GetRequiredService<ApplicationPaths>().GetConfigFilePath(ConfigFileNames.Shortcuts),
            sp.GetRequiredService<IShortcutHotkeyNormalizer>()));
        _ = services.AddSingleton<ShortcutService>();
        _ = services.AddSingleton<IShortcutService>(sp => sp.GetRequiredService<ShortcutService>());
        _ = services.AddSingleton<IShortcutTaskOperations>(sp => sp.GetRequiredService<ShortcutService>());
        _ = services.AddSingleton<IAutomationTaskStore<ShortcutTask>>(sp => sp.GetRequiredService<ShortcutService>());

        _ = services.AddSingleton<ITriggerTaskRepository>(sp => new JsonTriggerTaskRepository(sp.GetRequiredService<ApplicationPaths>().GetConfigFilePath(ConfigFileNames.Triggers)));
        _ = services.AddSingleton<ProfileSwitchRequestBridge>(sp => new ProfileSwitchRequestBridge(sp.GetRequiredService<IProfileSwitchRequestHandler>));
        _ = services.AddSingleton<IProfileSwitchRequests>(sp => sp.GetRequiredService<ProfileSwitchRequestBridge>());
        _ = services.AddSingleton<TriggerService>(sp => new TriggerService(
            sp.GetService<IWindowManager>(),
            sp.GetRequiredService<IProfileSwitchRequests>(),
            sp.GetRequiredService<IMacroFileManager>(),
            sp.GetRequiredService<Func<IMacroPlayer>>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ITriggerTaskRepository>()));
        _ = services.AddSingleton<ITriggerService>(sp => sp.GetRequiredService<TriggerService>());
        _ = services.AddSingleton<IAutomationTaskStore<TriggerTask>>(sp => sp.GetRequiredService<TriggerService>());

        _ = services.AddSingleton<IEditorActionConverter, EditorActionConverter>();
        _ = services.AddSingleton<IEditorActionValidator, EditorActionValidator>();
        _ = services.AddSingleton<ICoordinateCaptureService>(sp => new CoordinateCaptureService(sp.GetRequiredService<IMousePositionProvider>(), sp.GetService<Func<IInputCapture>>()));
    }
}
