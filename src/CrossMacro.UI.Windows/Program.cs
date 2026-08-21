
using CrossMacro.UI.Hosting;

namespace CrossMacro.UI.Windows;

[SupportedOSPlatform("windows")]
internal static class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => CrossMacro.UI.Program.BuildAvaloniaApp()
            .UseWin32()
            .UseSkia();

    [System.STAThread]
    public static Task<int> Main(string[] args)
    {
        return CliGuiRuntime.RunAsync(
            args,
            ConfigureGuiServices,
            ConfigureCliServices,
            startGui: () => CrossMacro.UI.Program.RunGui(
                args,
                ConfigureGuiServices,
                GuiHostBootstrap.ConfigureGuiRuntimeServices,
                static appBuilder => appBuilder.UseWin32().UseSkia()),
            getVersionString: CrossMacro.UI.Program.GetVersionString,
            tryAcquireSingleInstanceGuard: CrossMacro.UI.Program.TryAcquireRuntimeSingleInstanceGuard,
            bootstrapCallbacks: GuiHostBootstrap.CreateBootstrapCallbacks());
    }

    private static void ConfigurePlatformServices(IServiceCollection services)
    {
        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);
        _ = services.AddSingleton<IRuntimeContext, RuntimeContext>();
        GuiHostBootstrap.AddRuntimeDiagnostics(services);
    }

    private static void ConfigureGuiServices(IServiceCollection services)
    {
        ConfigurePlatformServices(services);
        GuiHostBootstrap.AddCommonGuiServices(services);
        WindowsPlatformServiceRegistrar.RegisterGuiClipboardServices(services);
    }

    private static void ConfigureCliServices(IServiceCollection services, CliRuntimeProfile runtimeProfile)
    {
        ConfigurePlatformServices(services);
        WindowsPlatformServiceRegistrar.RegisterCliClipboardServices(services);
        _ = services.AddCrossMacroCommonRuntimeServices();
        _ = services.AddCrossMacroSharedPostPlatformRuntimeServices(
            sp => runtimeProfile is CliRuntimeProfile.Persistent
                ? sp.GetService<IInputSimulatorPool>()
                : null);
        _ = services.AddCrossMacroMcp();
    }

}
