
namespace CrossMacro.UI.DependencyInjection;

/// <summary>
/// Extension methods for configuring CrossMacro services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Main entry point - registers all services for the application.
    /// </summary>
    public static IServiceCollection AddCrossMacroServices(
        this IServiceCollection services)
    {
        _ = services.AddCrossMacroGuiRuntimeServices();
        _ = services.AddViewModels();

        return services;
    }

    /// <summary>
    /// Registers runtime services for GUI execution.
    /// </summary>
    public static IServiceCollection AddCrossMacroGuiRuntimeServices(
        this IServiceCollection services)
    {
        services.TryAddSingleton(GuiStartupOptions.Default);
        GuiPresentationServiceRegistration.Register(services);
        GuiManagementServiceRegistration.Register(services);

        return services;
    }

    /// <summary>
    /// Registers all ViewModels.
    /// </summary>
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        ViewModelServiceRegistration.Register(services);

        return services;
    }

}
