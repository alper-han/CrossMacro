using CrossMacro.Application.DependencyInjection;

namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class WorkflowRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddCrossMacroApplicationServices();
        _ = services.AddSingleton<IScheduledTaskRepository>(sp => new JsonScheduledTaskRepository(sp.GetRequiredService<ApplicationPaths>().GetConfigFilePath(ConfigFileNames.Schedules)));
        _ = services.AddSingleton<IScheduledTaskExecutor, MacroScheduledTaskExecutor>();
        _ = services.AddSingleton<ISchedulerService, SchedulerService>();
        _ = services.AddSingleton<IScheduledTaskOperations>(sp => (IScheduledTaskOperations)sp.GetRequiredService<ISchedulerService>());
        _ = services.AddSingleton<IScheduledTaskStore>(sp => (IScheduledTaskStore)sp.GetRequiredService<ISchedulerService>());
        _ = services.AddSingleton<IShortcutTaskRepository>(sp => new JsonShortcutTaskRepository(sp.GetRequiredService<ApplicationPaths>().GetConfigFilePath(ConfigFileNames.Shortcuts)));
        _ = services.AddSingleton<ITriggerTaskRepository>(sp => new JsonTriggerTaskRepository(sp.GetRequiredService<ApplicationPaths>().GetConfigFilePath(ConfigFileNames.Triggers)));
        _ = services.AddSingleton<IShortcutService, ShortcutService>();
        _ = services.AddSingleton<IShortcutTaskOperations>(sp => (IShortcutTaskOperations)sp.GetRequiredService<IShortcutService>());
        _ = services.AddSingleton<IShortcutTaskStore>(sp => (IShortcutTaskStore)sp.GetRequiredService<IShortcutService>());
        _ = services.AddSingleton<ProfileSwitchRequestBridge>();
        _ = services.AddSingleton<IProfileSwitchRequests>(sp => sp.GetRequiredService<ProfileSwitchRequestBridge>());
        _ = services.AddSingleton<ITriggerService>(sp => new TriggerService(
            sp.GetService<IWindowManager>(),
            sp.GetRequiredService<IProfileSwitchRequests>(),
            sp.GetRequiredService<IMacroFileManager>(),
            sp.GetRequiredService<Func<IMacroPlayer>>(),
            triggersFilePath: null,
            timeProvider: sp.GetRequiredService<TimeProvider>(),
            taskRepository: sp.GetRequiredService<ITriggerTaskRepository>()));
        _ = services.AddSingleton<ITriggerTaskOperations>(sp => (ITriggerTaskOperations)sp.GetRequiredService<ITriggerService>());
        _ = services.AddSingleton<ITriggerTaskStore>(sp => (ITriggerTaskStore)sp.GetRequiredService<ITriggerService>());
        TextExpansionRuntimeServiceRegistration.Register(services);
        _ = services.AddSingleton<IEditorActionConverter, EditorActionConverter>();
        _ = services.AddSingleton<IEditorActionValidator, EditorActionValidator>();
        _ = services.AddSingleton<ICoordinateCaptureService>(sp => new CoordinateCaptureService(sp.GetRequiredService<IMousePositionProvider>(), sp.GetService<Func<IInputCapture>>()));
        ProfileRuntimeServiceRegistration.Register(services);
    }
}
