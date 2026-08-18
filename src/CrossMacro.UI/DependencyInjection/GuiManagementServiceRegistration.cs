namespace CrossMacro.UI.DependencyInjection;

internal static class GuiManagementServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<IManageProfile, ManageProfile>();
        _ = services.AddSingleton<IManageTextExpansion, ManageTextExpansion>();
        _ = services.AddSingleton<IManageSchedule, ManageSchedule>();
        _ = services.AddSingleton<IManageShortcut, ManageShortcut>();
        _ = services.AddSingleton<IManageTrigger, ManageTrigger>();
    }
}
