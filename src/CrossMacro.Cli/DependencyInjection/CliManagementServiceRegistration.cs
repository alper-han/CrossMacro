namespace CrossMacro.Cli.DependencyInjection;

internal static class CliManagementServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ISettingsCliService>(sp => new SettingsCliService(
            sp.GetRequiredService<ISettingsService>(),
            sp.GetService<IPortalScreenCastRestoreStateService>()));
        _ = services.AddSingleton<IQuickSetupCliService, QuickSetupCliService>();
        _ = services.AddSingleton<IManageProfile, ManageProfile>();
        _ = services.AddSingleton<IManageTextExpansion, ManageTextExpansion>();
        _ = services.AddSingleton<IManageShortcut, ManageShortcut>();
        _ = services.AddSingleton<IManageSchedule, ManageSchedule>();
        _ = services.AddSingleton<IManageTrigger, ManageTrigger>();
        _ = services.AddSingleton<IProfileCliService, ProfileCliService>();
        _ = services.AddSingleton<ITextExpansionCliService, TextExpansionCliService>();
        _ = services.AddSingleton<IScheduleCliService>(sp => new ScheduleCliService(sp.GetRequiredService<IManageSchedule>()));
        _ = services.AddSingleton<IShortcutCliService>(sp => new ShortcutCliService(sp.GetRequiredService<IManageShortcut>()));
        _ = services.AddSingleton<ITriggerCliService>(sp => new TriggerCliService(sp.GetRequiredService<IManageTrigger>()));
    }
}
