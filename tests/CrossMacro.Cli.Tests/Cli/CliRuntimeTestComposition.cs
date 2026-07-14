using CrossMacro.Cli;
using CrossMacro.Infrastructure.DependencyInjection;
using CrossMacro.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Cli.Tests;

internal static class CliRuntimeTestComposition
{
    internal static IServiceCollection AddCrossMacroCliRuntimeServices(
        this IServiceCollection services,
        IPlatformServiceRegistrar registrar,
        CliRuntimeProfile runtimeProfile = CliRuntimeProfile.OneShot)
    {
        registrar.RegisterPlatformServices(services);
        services.AddCrossMacroCommonRuntimeServices();
        services.AddCrossMacroSharedPostPlatformRuntimeServices(
            sp => runtimeProfile == CliRuntimeProfile.Persistent
                ? sp.GetService<IInputSimulatorPool>()
                : null);
        return services;
    }
}
