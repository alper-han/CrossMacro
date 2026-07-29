namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class WorkflowRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<IScheduledTaskRepository, JsonScheduledTaskRepository>();
        _ = services.AddSingleton<IScheduledTaskExecutor, MacroScheduledTaskExecutor>();
        _ = services.AddSingleton<ISchedulerService, SchedulerService>();
        _ = services.AddSingleton<IShortcutService, ShortcutService>();
        _ = services.AddSingleton<ProfileSwitchRequestBridge>();
        _ = services.AddSingleton<IProfileSwitchRequests>(sp => sp.GetRequiredService<ProfileSwitchRequestBridge>());
        _ = services.AddSingleton<ITriggerService>(sp => new TriggerService(sp.GetService<IWindowManager>(), sp.GetRequiredService<IProfileSwitchRequests>(), sp.GetRequiredService<IMacroFileManager>(), sp.GetRequiredService<Func<IMacroPlayer>>()));
        _ = services.AddSingleton<ITextExpansionStorageService, TextExpansionStorageService>();
        _ = services.AddSingleton<ITextExpansionStore>(sp => sp.GetRequiredService<ITextExpansionStorageService>());
        _ = services.AddSingleton<IInputProcessor, InputProcessor>();
        _ = services.AddSingleton<ITextBufferState, TextBufferState>();
        _ = services.AddSingleton<ITextExpansionExecutor, TextExpansionExecutor>();
        _ = services.AddSingleton<ITextExpansionService, TextExpansionService>();
        _ = services.AddSingleton<IEditorActionConverter, EditorActionConverter>();
        _ = services.AddSingleton<IEditorActionValidator, EditorActionValidator>();
        _ = services.AddSingleton<ICoordinateCaptureService>(sp => new CoordinateCaptureService(sp.GetRequiredService<IMousePositionProvider>(), sp.GetService<Func<IInputCapture>>()));
        _ = services.AddSingleton<IProfileCatalog>(_ => new ProfileManager(configRootPath: null));
        _ = services.AddSingleton<IProfileManager>(sp =>
        {
            var hasKeyboardLayout = sp.GetService<IKeyboardLayoutService>() is not null;
            var hasInputCaptureFactory = sp.GetService<Func<IInputCapture>>() is not null;
            var coordinator = new ProfileRuntimeCoordinator(sp.GetRequiredService<IProfileCatalog>(), sp.GetRequiredService<ISettingsService>(), sp.GetRequiredService<IHotkeyConfigurationService>(), sp.GetRequiredService<HotkeySettings>(), hasKeyboardLayout ? sp.GetRequiredService<IGlobalHotkeyService>() : null, hasKeyboardLayout ? sp.GetRequiredService<IShortcutService>() : null, sp.GetRequiredService<ISchedulerService>(), hasInputCaptureFactory ? sp.GetRequiredService<ITextExpansionService>() : null, sp.GetRequiredService<ITriggerService>(), sp.GetRequiredService<IScheduledTaskRepository>(), sp.GetRequiredService<ITextExpansionStorageService>());
            sp.GetRequiredService<ProfileSwitchRequestBridge>().SetHandler(coordinator);
            return coordinator;
        });
    }
}
