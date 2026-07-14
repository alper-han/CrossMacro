using Avalonia;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.DependencyInjection;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Logging;
using CrossMacro.Platform.MacOS.DependencyInjection;
using CrossMacro.Platform.Abstractions;
using CrossMacro.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

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
                ConfigureGuiRuntimeServices,
                static appBuilder => appBuilder.UseAvaloniaNative().UseSkia()),
            getVersionString: CrossMacro.UI.Program.GetVersionString,
            tryAcquireSingleInstanceGuard: CrossMacro.UI.Program.TryAcquireRuntimeSingleInstanceGuard,
            bootstrapCallbacks: new CliBootstrapCallbacks(ConfigureInitialLogging, ConfigureCommandLogging, ConfigureHostLogging));
    }

    private static void ConfigurePlatformServices(IServiceCollection services)
    {
        new MacOSPlatformServiceRegistrar().RegisterPlatformServices(services);
        services.AddSingleton<IRuntimeContext, RuntimeContext>();
        services.AddSingleton<IDisplayEnvironmentDiagnostic>(sp =>
            (IDisplayEnvironmentDiagnostic)sp.GetRequiredService<IRuntimeContext>());
        services.AddSingleton<IRuntimeLogLevelService, RuntimeLogLevelService>();
    }

    private static void ConfigureGuiServices(IServiceCollection services)
    {
        ConfigurePlatformServices(services);
        services.AddSingleton<AvaloniaClipboardService>();
        services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<AvaloniaClipboardService>());
        services.AddSingleton<IUpdateService, GitHubUpdateService>();
        services.AddSingleton<IImageClipboardService, NoOpImageClipboardService>();
        services.AddSingleton<Func<CancellationToken, Task>>(sp =>
            token => sp.GetRequiredService<IScreenReadingWarmupService>().WarmUpPortalSessionAsync(token));
    }

    private static void ConfigureCliServices(IServiceCollection services, CliRuntimeProfile runtimeProfile)
    {
        ConfigurePlatformServices(services);
        services.AddSingleton<IClipboardService, CrossMacro.Cli.Services.NoOpClipboardService>();
        services.AddSingleton<IImageClipboardService, NoOpImageClipboardService>();
        services.AddCrossMacroCommonRuntimeServices();
        services.AddCrossMacroSharedPostPlatformRuntimeServices(
            sp => runtimeProfile == CliRuntimeProfile.Persistent
                ? sp.GetService<IInputSimulatorPool>()
                : null);
    }

    private static void ConfigureGuiRuntimeServices(IServiceCollection services)
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
}
