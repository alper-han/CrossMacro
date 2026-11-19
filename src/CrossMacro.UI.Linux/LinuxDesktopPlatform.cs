using System.Runtime.Versioning;
using Avalonia;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Linux.DependencyInjection;
using CrossMacro.Platform.Linux.Services.Runtime;
using CrossMacro.UI.Hosting;
using CrossMacro.UI.Linux.Services;
using CrossMacro.UI.Services.Desktop;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.UI.Linux;

[SupportedOSPlatform("linux")]
internal sealed class LinuxDesktopPlatform(LinuxEnvironmentSnapshot environment) : IDesktopPlatform
{
    private readonly LinuxPlatformServiceRegistrar _registrar = new(environment);

    public void RegisterPlatformServices(IServiceCollection services) => _registrar.RegisterPlatformServices(services);

    public void RegisterGuiServices(IServiceCollection services) =>
        _ = services.AddSingleton<ITrayIconBackend, LinuxStatusNotifierTrayBackend>();

    public AppBuilder ConfigureAppBuilder(AppBuilder builder) =>
        UseLinuxWindowingSubsystem(builder).UseSkia();

    internal static bool ShouldUseWayland(LinuxEnvironmentSnapshot environment) =>
        string.Equals(environment.SessionType, "wayland", StringComparison.OrdinalIgnoreCase) ||
        (!string.Equals(environment.SessionType, "x11", StringComparison.OrdinalIgnoreCase) &&
         !string.IsNullOrEmpty(environment.WaylandDisplay));

    private AppBuilder UseLinuxWindowingSubsystem(AppBuilder builder)
    {
        _ = builder.UseStandardRuntimePlatformSubsystem();

        _ = builder.UseWindowingSubsystem(() =>
        {
            if (ShouldUseWayland(environment))
            {
                try
                {
                    _ = builder.UseWayland();
                    builder.WindowingSubsystemInitializer?.Invoke();
                    return;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.Warning(ex, "Wayland initialization failed, falling back to X11");
                }
            }

            _ = builder.UseX11();
            builder.WindowingSubsystemInitializer?.Invoke();
        }, "Wayland/X11 Fallback");

        return builder;
    }
}
