
using CrossMacro.UI.Hosting;

namespace CrossMacro.UI.MacOS;

[SupportedOSPlatform("macos")]
internal static class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => CrossMacro.UI.Program.BuildAvaloniaApp()
            .UseAvaloniaNative()
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
                static appBuilder => appBuilder.UseAvaloniaNative().UseSkia()),
            getVersionString: CrossMacro.UI.Program.GetVersionString,
            tryAcquireSingleInstanceGuard: CrossMacro.UI.Program.TryAcquireRuntimeSingleInstanceGuard,
            bootstrapCallbacks: GuiHostBootstrap.CreateBootstrapCallbacks());
    }

    private static void ConfigurePlatformServices(IServiceCollection services)
    {
        new MacOSPlatformServiceRegistrar().RegisterPlatformServices(services);
        _ = services.AddSingleton<IRuntimeContext, RuntimeContext>();
        GuiHostBootstrap.AddRuntimeDiagnostics(services);
    }

    private static void ConfigureGuiServices(IServiceCollection services)
    {
        ConfigurePlatformServices(services);
        GuiHostBootstrap.AddCommonGuiServices(services);
        RegisterNativeClipboardServices(services);
    }

    private static void ConfigureCliServices(IServiceCollection services, CliRuntimeProfile runtimeProfile)
    {
        ConfigurePlatformServices(services);
        RegisterNativeClipboardServices(services);
        _ = services.AddCrossMacroCommonRuntimeServices();
        _ = services.AddCrossMacroSharedPostPlatformRuntimeServices(
            sp => runtimeProfile is CliRuntimeProfile.Persistent
                ? sp.GetService<IInputSimulatorPool>()
                : null);
        _ = services.AddCrossMacroMcp();
    }

    private static void RegisterNativeClipboardServices(IServiceCollection services)
    {
        MacOSPlatformServiceRegistrar.RegisterNativeClipboardServices(services);
    }

}
