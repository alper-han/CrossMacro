
namespace CrossMacro.Cli.Tests;

internal static class CliRuntimeTestComposition
{
    /// <summary>Composes the CLI runtime against <paramref name="applicationPaths"/> so no test touches the real user profile.</summary>
    internal static IServiceCollection AddCrossMacroCliRuntimeServices(
        this IServiceCollection services,
        IPlatformServiceRegistrar registrar,
        ApplicationPaths applicationPaths,
        CliRuntimeProfile runtimeProfile = CliRuntimeProfile.OneShot)
    {
        _ = services.AddSingleton(applicationPaths);
        registrar.RegisterPlatformServices(services);
        _ = services.AddCrossMacroCommonRuntimeServices();
        _ = services.AddCrossMacroSharedPostPlatformRuntimeServices(
            sp => runtimeProfile is CliRuntimeProfile.Persistent
                ? sp.GetService<IInputSimulatorPool>()
                : null);
        return services;
    }
}
