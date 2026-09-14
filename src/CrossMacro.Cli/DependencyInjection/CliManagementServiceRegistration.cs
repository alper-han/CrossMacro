using CrossMacro.Application.DependencyInjection;

namespace CrossMacro.Cli.DependencyInjection;

internal static class CliManagementServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddCrossMacroApplicationServices();
        _ = services.AddSingleton<ISettingsCliService>(sp => new SettingsCliService(
            sp.GetRequiredService<ISettingsService>(),
            sp.GetService<IPortalScreenCastRestoreStateService>(),
            sp.GetRequiredService<CrossMacro.Application.Settings.SettingsChangeCoordinator>()));
        _ = services.AddSingleton<IQuickSetupCliService, QuickSetupCliService>();
        _ = services.AddSingleton<IProfileCliService>(sp => new ProfileCliService(
            sp.GetRequiredService<CrossMacro.Application.Profiles.IProfileOperations>()));
        _ = services.AddSingleton<ITextExpansionCliService, TextExpansionCliService>();
        _ = services.AddSingleton<IScheduleCliService>(sp => new ScheduleCliService(sp.GetRequiredService<IScheduleCommands>()));
        _ = services.AddSingleton<IShortcutCliService>(sp => new ShortcutCliService(sp.GetRequiredService<IShortcutCommands>()));
        _ = services.AddSingleton<ITriggerCliService>(sp => new TriggerCliService(sp.GetRequiredService<ITriggerCommands>()));
    }
}
