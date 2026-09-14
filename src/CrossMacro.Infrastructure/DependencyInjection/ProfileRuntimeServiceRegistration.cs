using CrossMacro.Application.Settings;

namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class ProfileRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ProfileRuntimeState>();
        _ = services.AddSingleton<IProfileRuntimeState>(sp => sp.GetRequiredService<ProfileRuntimeState>());
        _ = services.AddSingleton<IProfileCatalog>(sp => new ProfileManager(
            sp.GetRequiredService<ApplicationPaths>().ConfigDirectory,
            sp.GetRequiredService<TimeProvider>()));
        _ = services.AddSingleton<AutomationRuntimeServices>(ResolveAutomationServices);
        _ = services.AddSingleton<AutomationRuntimeSession>(sp =>
        {
            var runtime = sp.GetRequiredService<AutomationRuntimeServices>();
            return AutomationRuntimeSessionFactory.Create(
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<HotkeySettings>(),
                runtime.Hotkeys, runtime.Shortcuts, runtime.Scheduler, runtime.Triggers, runtime.TextExpansion);
        });
        _ = services.AddSingleton<IProfileManager>(CreateProfileManager);
    }

    private static IProfileManager CreateProfileManager(IServiceProvider services)
    {
        var runtime = services.GetRequiredService<AutomationRuntimeServices>();
        var coordinator = new ProfileRuntimeCoordinator(
            services.GetRequiredService<IProfileCatalog>(),
            services.GetRequiredService<ISettingsService>(),
            services.GetRequiredService<IHotkeyConfigurationService>(),
            services.GetRequiredService<HotkeySettings>(),
            runtime.Hotkeys,
            runtime.Shortcuts,
            runtime.Scheduler,
            runtime.TextExpansion,
            runtime.Triggers,
            services.GetRequiredService<IScheduledTaskRepository>(),
            services.GetRequiredService<ITextExpansionStorageService>(),
            services.GetRequiredService<ProfileRuntimeState>(),
            services.GetServices<IProfileRuntimeParticipant>(),
            services.GetRequiredService<AutomationRuntimeSession>(),
            services.GetRequiredService<SettingsChangeCoordinator>(),
            services.GetRequiredService<AutomationTaskMutationGate>());
        services.GetRequiredService<ProfileSwitchRequestBridge>().SetHandler(coordinator);
        return coordinator;
    }

    private static AutomationRuntimeServices ResolveAutomationServices(IServiceProvider services)
    {
        var hasKeyboardLayout = services.GetService<IKeyboardLayoutService>() is not null;
        var hasTextExpansionDependencies = services.GetService<IClipboardService>() is not null
            && hasKeyboardLayout
            && services.GetService<Func<IInputCapture>>() is not null
            && services.GetService<Func<IInputSimulator>>() is not null;
        return new AutomationRuntimeServices(
            hasKeyboardLayout ? services.GetRequiredService<IGlobalHotkeyService>() : null,
            hasKeyboardLayout ? services.GetRequiredService<IShortcutService>() : null,
            services.GetRequiredService<ISchedulerService>(),
            services.GetRequiredService<ITriggerService>(),
            hasTextExpansionDependencies ? services.GetRequiredService<ITextExpansionService>() : null);
    }

    // Startup and profile reload use the same optional services in a host session.
    private sealed record AutomationRuntimeServices(
        IGlobalHotkeyService? Hotkeys,
        IShortcutService? Shortcuts,
        ISchedulerService Scheduler,
        ITriggerService Triggers,
        ITextExpansionService? TextExpansion);
}
