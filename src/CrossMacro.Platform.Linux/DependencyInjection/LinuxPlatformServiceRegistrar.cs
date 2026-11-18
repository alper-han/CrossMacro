
namespace CrossMacro.Platform.Linux.DependencyInjection;

/// <summary>
/// Linux platform service registrar.
/// Handles Wayland/X11/legacy fallback service selection and supplies the
/// Linux runtime context and native clipboard ports.
/// </summary>
public sealed class LinuxPlatformServiceRegistrar(LinuxEnvironmentSnapshot environment) : IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddSingleton(typeof(LinuxEnvironmentSnapshot), environment);
        _ = services.AddSingleton<IRuntimeContext>(new LinuxRuntimeContext(environment));
        services.AddLinuxCoreServices(environment);
        services.AddLinuxLegacyImplementations();
        services.AddLinuxIpcImplementations();
        services.AddLinuxX11Implementations();
        services.AddLinuxFactories();
        services.AddLinuxInputFactories();
        services.AddLinuxCoordinateStrategy();
        services.AddLinuxInputSimulatorPool();
        RegisterNativeClipboardServices(services);
    }

    private static void RegisterNativeClipboardServices(IServiceCollection services)
    {
        _ = services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
        _ = services.AddSingleton<ILinuxClipboardService>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
        _ = services.AddSingleton<IImageClipboardService>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
        _ = services.AddSingleton<IImageClipboardReader>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
    }
}
