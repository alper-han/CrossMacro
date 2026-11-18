using CrossMacro.Commands.DependencyInjection;

namespace CrossMacro.Cli.DependencyInjection;

public static class CliServiceCollectionExtensions
{
    public static IServiceCollection AddCliServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddCrossMacroCommandServices();
        _ = services.AddSingleton<IHeadlessHotkeyActionService, HeadlessHotkeyActionService>();
        _ = services.AddSingleton<IHeadlessRuntimeService, HeadlessRuntimeService>();
        CliCommandHandlerRegistrationModule.Register(services);
        return services;
    }
}
