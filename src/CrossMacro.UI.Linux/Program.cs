
namespace CrossMacro.UI.Linux;

internal static class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => CrossMacro.UI.Program.BuildAvaloniaApp()
            .UseLinuxWindowingSubsystem(LinuxEnvironmentVariables.CaptureCurrentSnapshot())
            .UseSkia();

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
                ConfigureGuiRuntimeServices,
                appBuilder => appBuilder
                    .UseLinuxWindowingSubsystem(environment)
                    .UseSkia()),
            getVersionString: CrossMacro.UI.Program.GetVersionString,
            tryAcquireSingleInstanceGuard: CrossMacro.UI.Program.TryAcquireRuntimeSingleInstanceGuard,
            bootstrapCallbacks: new CliBootstrapCallbacks(ConfigureInitialLogging, ConfigureCommandLogging, ConfigureHostLogging));
    }

    private static void ConfigurePlatformServices(IServiceCollection services, LinuxEnvironmentSnapshot environment)
    {
        new LinuxPlatformServiceRegistrar().RegisterPlatformServices(services, environment);
        services.AddSingleton<IRuntimeContext>(new LinuxRuntimeContext(environment));
        services.AddSingleton<IDisplayEnvironmentDiagnostic>(sp =>
            (IDisplayEnvironmentDiagnostic)sp.GetRequiredService<IRuntimeContext>());
        services.AddSingleton<IRuntimeLogLevelService, RuntimeLogLevelService>();
    }

    internal static void ConfigureGuiServices(IServiceCollection services, LinuxEnvironmentSnapshot environment)
    {
        ConfigurePlatformServices(services, environment);
        services.AddSingleton<AvaloniaClipboardService>();
        services.AddSingleton<PlatformProcessRunner, ProcessRunner>();
        services.AddSingleton<IHostClipboardService>(sp => new PlatformFlatpakClipboard(
            sp.GetRequiredService<PlatformProcessRunner>(),
            sp.GetRequiredService<IRuntimeContext>(),
            environment));
        services.AddSingleton<ILinuxClipboardService>(sp => new PlatformLinuxClipboard(
            sp.GetRequiredService<PlatformProcessRunner>(), environment));
        services.AddSingleton<PlatformFlatpakImageClipboard>(sp => new PlatformFlatpakImageClipboard(
            sp.GetRequiredService<PlatformProcessRunner>(),
            sp.GetRequiredService<IRuntimeContext>(),
            environment));
        services.AddSingleton<PlatformLinuxImageClipboard>(sp => new PlatformLinuxImageClipboard(
            sp.GetRequiredService<PlatformProcessRunner>(), environment));
        services.AddSingleton<IClipboardService, CompositeClipboardService>();
        services.AddSingleton<IUpdateService, GitHubUpdateService>();
        services.AddSingleton<IImageClipboardService>(sp =>
            sp.GetRequiredService<IRuntimeContext>().IsFlatpak
                ? sp.GetRequiredService<PlatformFlatpakImageClipboard>()
                : sp.GetRequiredService<PlatformLinuxImageClipboard>());
        services.AddSingleton<Func<CancellationToken, Task>>(sp =>
            token => sp.GetRequiredService<IScreenReadingWarmupService>().WarmUpPortalSessionAsync(token));
    }

    private static void ConfigureCliServices(
        IServiceCollection services,
        LinuxEnvironmentSnapshot environment,
        CliRuntimeProfile runtimeProfile)
    {
        ConfigurePlatformServices(services, environment);
        services.AddSingleton<PlatformProcessRunner, ProcessRunner>();
        services.AddSingleton<ILinuxClipboardService>(sp => new PlatformLinuxClipboard(
            sp.GetRequiredService<PlatformProcessRunner>(), environment));
        services.AddSingleton<PlatformLinuxImageClipboard>(sp => new PlatformLinuxImageClipboard(
            sp.GetRequiredService<PlatformProcessRunner>(), environment));
        services.AddSingleton<PlatformFlatpakImageClipboard>(sp => new PlatformFlatpakImageClipboard(
            sp.GetRequiredService<PlatformProcessRunner>(),
            sp.GetRequiredService<IRuntimeContext>(),
            environment));
        services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<ILinuxClipboardService>());
        services.AddSingleton<IImageClipboardService>(sp =>
            sp.GetRequiredService<IRuntimeContext>().IsFlatpak
                ? sp.GetRequiredService<PlatformFlatpakImageClipboard>()
                : sp.GetRequiredService<PlatformLinuxImageClipboard>());
        services.AddCrossMacroCommonRuntimeServices();
        services.AddCrossMacroSharedPostPlatformRuntimeServices(
            sp => runtimeProfile is CliRuntimeProfile.Persistent
                ? sp.GetService<IInputSimulatorPool>()
                : null);
    }

    internal static void ConfigureGuiRuntimeServices(IServiceCollection services)
    {
        services.AddCrossMacroCommonRuntimeServices();
        services.AddCrossMacroSharedPostPlatformRuntimeServices(sp => sp.GetService<IInputSimulatorPool>());
    }

    private static void ConfigureInitialLogging(CliParseResult parseResult)
    {
        var json = parseResult.PrefersJsonOutput;
        LoggerSetup.Initialize(json ? "Fatal" : SettingsService.TryLoadLogLevelEarly(), !json, !json);
    }

    private static void ConfigureCommandLogging(CliCommandOptions options)
    {
        LoggerSetup.SetLogLevel(options.JsonOutput
            ? "Fatal"
            : string.IsNullOrWhiteSpace(options.LogLevel) ? "Warning" : options.LogLevel);
    }

    private static void ConfigureHostLogging(CliCommandOptions options)
    {
        if (options.JsonOutput)
        {
            LoggerSetup.Initialize("Fatal", enableFileLogging: false, enableConsoleLogging: false);
        }
    }

    internal static string SelectLinuxWindowingBackend(LinuxEnvironmentSnapshot environment) =>
        string.Equals(environment.SessionType, "wayland", StringComparison.OrdinalIgnoreCase) ||
        (!string.Equals(environment.SessionType, "x11", StringComparison.OrdinalIgnoreCase) &&
         !string.IsNullOrEmpty(environment.WaylandDisplay))
            ? "Wayland"
            : "X11";

    private static AppBuilder UseLinuxWindowingSubsystem(this AppBuilder builder, LinuxEnvironmentSnapshot environment)
    {
        builder.UseStandardRuntimePlatformSubsystem();

        builder.UseWindowingSubsystem(() =>
        {
            if (SelectLinuxWindowingBackend(environment) is "Wayland")
            {
                try
                {
                    builder.UseWayland();
                    builder.WindowingSubsystemInitializer?.Invoke();
                    return;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Wayland initialization failed, falling back to X11");
                }
            }

            builder.UseX11();
            builder.WindowingSubsystemInitializer?.Invoke();
        }, "Wayland/X11 Fallback");

        return builder;
    }
}
