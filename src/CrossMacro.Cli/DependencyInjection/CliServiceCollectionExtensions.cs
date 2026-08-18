
namespace CrossMacro.Cli.DependencyInjection;

public static class CliServiceCollectionExtensions
{
    public static IServiceCollection AddCliServices(this IServiceCollection services)
    {
        CliPreflightServiceRegistration.Register(services);
        CliManagementServiceRegistration.Register(services);
        CliOperationalServiceRegistration.Register(services);
        CliCommandHandlerRegistrationModule.Register(services);
        return services;
    }
}
