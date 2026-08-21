
using System.Runtime.Versioning;
using CrossMacro.UI.Hosting;

namespace CrossMacro.UI.Linux;

[SupportedOSPlatform("linux")]
internal static class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => ConfigureLinuxGuiBuilder(
            CrossMacro.UI.Program.BuildAvaloniaApp(),
            LinuxEnvironmentVariables.CaptureCurrentSnapshot());

    private static AppBuilder ConfigureLinuxGuiBuilder(
        AppBuilder builder,
        LinuxEnvironmentSnapshot environment)
    {
        return builder
            .UseLinuxWindowingSubsystem(environment)
            .UseSkia();
    }

    [System.STAThread]
    public static Task<int> Main(string[] args)
    {
        var environment = LinuxEnvironmentVariables.CaptureCurrentSnapshot();
        return CliGuiRuntime.RunAsync(
            args,
            services => ConfigureGuiServices(services, environment),
            (services, profile) => ConfigureCliServices(services, environment, profile),
            startGui: () => CrossMacro.UI.Program.RunGui(
                args,
                services => ConfigureGuiServices(services, environment),
                GuiHostBootstrap.ConfigureGuiRuntimeServices,
                appBuilder => ConfigureLinuxGuiBuilder(appBuilder, environment)),
            getVersionString: CrossMacro.UI.Program.GetVersionString,
            tryAcquireSingleInstanceGuard: CrossMacro.UI.Program.TryAcquireRuntimeSingleInstanceGuard,
            bootstrapCallbacks: GuiHostBootstrap.CreateBootstrapCallbacks());
    }

    private static void ConfigurePlatformServices(IServiceCollection services, LinuxEnvironmentSnapshot environment)
    {
        LinuxPlatformServiceRegistrar.RegisterPlatformServices(services, environment);
        _ = services.AddSingleton<IRuntimeContext>(new LinuxRuntimeContext(environment));
        GuiHostBootstrap.AddRuntimeDiagnostics(services);
    }

    internal static void ConfigureGuiServices(IServiceCollection services, LinuxEnvironmentSnapshot environment)
    {
        ConfigurePlatformServices(services, environment);
        GuiHostBootstrap.AddCommonGuiServices(services);
        _ = services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
        _ = services.AddSingleton<ILinuxClipboardService>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
        _ = services.AddSingleton<IImageClipboardService>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
        _ = services.AddSingleton<IImageClipboardReader>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
    }

    private static void ConfigureCliServices(
        IServiceCollection services,
        LinuxEnvironmentSnapshot environment,
        CliRuntimeProfile runtimeProfile)
    {
        ConfigurePlatformServices(services, environment);
        _ = services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
        _ = services.AddSingleton<ILinuxClipboardService>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
        _ = services.AddSingleton<IImageClipboardService>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
        _ = services.AddSingleton<IImageClipboardReader>(sp => sp.GetRequiredService<LinuxNativeClipboardService>());
        _ = services.AddCrossMacroCommonRuntimeServices();
        _ = services.AddCrossMacroSharedPostPlatformRuntimeServices(
            sp => runtimeProfile is CliRuntimeProfile.Persistent
                ? sp.GetService<IInputSimulatorPool>()
                : null);
        _ = services.AddCrossMacroMcp();
    }

    internal static string SelectLinuxWindowingBackend(LinuxEnvironmentSnapshot environment) =>
        string.Equals(environment.SessionType, "wayland", StringComparison.OrdinalIgnoreCase) ||
        (!string.Equals(environment.SessionType, "x11", StringComparison.OrdinalIgnoreCase) &&
         !string.IsNullOrEmpty(environment.WaylandDisplay))
            ? "Wayland"
            : "X11";

    private static AppBuilder UseLinuxWindowingSubsystem(this AppBuilder builder, LinuxEnvironmentSnapshot environment)
    {
        _ = builder.UseStandardRuntimePlatformSubsystem();

        _ = builder.UseWindowingSubsystem(() =>
        {
            if (SelectLinuxWindowingBackend(environment) is "Wayland")
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
