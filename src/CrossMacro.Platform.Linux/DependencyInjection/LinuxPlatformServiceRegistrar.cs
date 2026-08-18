
namespace CrossMacro.Platform.Linux.DependencyInjection;

/// <summary>
/// Linux platform service registrar.
/// Handles Wayland/X11/legacy fallback service selection.
/// </summary>
public sealed class LinuxPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services)
    {
        RegisterPlatformServices(services, LinuxEnvironmentVariables.CaptureCurrentSnapshot());
    }

    public static void RegisterPlatformServices(IServiceCollection services, LinuxEnvironmentSnapshot environment)
    {
        _ = services.AddSingleton(typeof(LinuxEnvironmentSnapshot), environment);
        services.AddLinuxCoreServices(environment);
        services.AddLinuxLegacyImplementations();
        services.AddLinuxIpcImplementations();
        services.AddLinuxX11Implementations();
        services.AddLinuxFactories();
        services.AddLinuxInputFactories();
        services.AddLinuxStrategySelectors();
        services.AddLinuxPositionProviderSelectors();
        services.AddLinuxCoordinateStrategy();
        services.AddLinuxInputSimulatorPool();
    }
}
