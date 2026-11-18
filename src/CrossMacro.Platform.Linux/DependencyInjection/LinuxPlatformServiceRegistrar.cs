using CrossMacro.Core.Services.Clipboard;
using CrossMacro.Platform.Linux.Clipboard;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.Linux.DependencyInjection;

/// <summary>
/// Linux platform service registrar.
/// Handles Wayland/X11/direct-device fallback service selection and supplies the
/// Linux runtime context and native clipboard ports.
/// </summary>
public sealed class LinuxPlatformServiceRegistrar(LinuxEnvironmentSnapshot environment) : IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddSingleton(typeof(LinuxEnvironmentSnapshot), environment);
        var runtimeContext = new LinuxRuntimeContext(environment);
        _ = services.AddSingleton(runtimeContext);
        _ = services.AddSingleton<IRuntimeContext>(runtimeContext);
        services.AddLinuxCoreServices(environment);
        services.AddLinuxDirectDeviceImplementations();
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
        _ = services.AddSingleton<IImageClipboardService>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
        _ = services.AddSingleton<IImageClipboardReader>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
    }
}
