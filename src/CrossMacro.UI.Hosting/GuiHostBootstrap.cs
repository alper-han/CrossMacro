using CrossMacro.Cli;
using CrossMacro.Cli.Options;
using CrossMacro.Core.Services.Runtime;
using CrossMacro.Core.Services.Updates;
using CrossMacro.Infrastructure.DependencyInjection;
using CrossMacro.Infrastructure.Logging;
using CrossMacro.Infrastructure.Services.Runtime;
using CrossMacro.Infrastructure.Services.Settings;
using CrossMacro.Infrastructure.Services.Updates;
using CrossMacro.Mcp.DependencyInjection;
using CrossMacro.Platform.Abstractions.Input.Simulation;
using CrossMacro.Platform.Abstractions.Runtime;
using CrossMacro.Platform.Abstractions.ScreenReading;
using CrossMacro.UI.Services.Clipboard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrossMacro.UI.Hosting;

/// <summary>
/// The composition root shared by the Linux, Windows, and macOS executables. Each executable
/// supplies an <see cref="IDesktopPlatform"/>; everything else is composed here.
/// </summary>
public static class GuiHostBootstrap
{
    public static Task<int> RunAsync(ReadOnlyMemory<string> args, IDesktopPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        return CliGuiRuntime.RunAsync(
            args,
            (services, runtimeProfile) => services.AddCrossMacroCliRuntimeServices(platform, runtimeProfile),
            startGui: () => CrossMacro.UI.Program.RunGui(
                args.Span,
                services => services.AddCrossMacroGuiHostServices(platform),
                platform.ConfigureAppBuilder),
            getVersionString: CrossMacro.UI.Program.GetVersionString,
            tryAcquireSingleInstanceGuard: CrossMacro.UI.Program.TryAcquireRuntimeSingleInstanceGuard,
            bootstrapCallbacks: new CliBootstrapCallbacks(ConfigureInitialLogging, ConfigureCommandLogging),
            tryActivateExistingInstance: CrossMacro.UI.Program.TryActivateExistingRuntime);
    }

    /// <summary>
    /// Composes the runtime graph for CLI, headless, and MCP commands. Only the persistent
    /// profile shares the platform's input simulator pool.
    /// </summary>
    public static IServiceCollection AddCrossMacroCliRuntimeServices(
        this IServiceCollection services,
        IPlatformServiceRegistrar registrar,
        CliRuntimeProfile runtimeProfile)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registrar);

        AddPlatformServices(services, registrar);
        _ = services.AddCrossMacroCommonRuntimeServices();
        _ = services.AddCrossMacroSharedPostPlatformRuntimeServices(
            sp => runtimeProfile is CliRuntimeProfile.Persistent
                ? sp.GetService<IInputSimulatorPool>()
                : null);
        _ = services.AddCrossMacroMcp();
        return services;
    }

    /// <summary>
    /// Composes everything the GUI graph needs beneath the UI project's own
    /// <c>AddCrossMacroServices</c> registrations.
    /// </summary>
    public static IServiceCollection AddCrossMacroGuiHostServices(
        this IServiceCollection services,
        IDesktopPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(platform);

        AddPlatformServices(services, platform);
        platform.RegisterGuiServices(services);
        _ = services.AddSingleton<AvaloniaClipboardService>();
        _ = services.AddSingleton<IUpdateService, GitHubUpdateService>();
        _ = services.AddSingleton<Func<CancellationToken, Task>>(sp =>
            token => sp.GetRequiredService<IScreenReadingWarmupService>().WarmUpPortalSessionAsync(token));
        _ = services.AddCrossMacroCommonRuntimeServices();
        _ = services.AddCrossMacroSharedPostPlatformRuntimeServices(sp => sp.GetService<IInputSimulatorPool>());
        return services;
    }

    private static void AddPlatformServices(IServiceCollection services, IPlatformServiceRegistrar registrar)
    {
        registrar.RegisterPlatformServices(services);
        services.TryAddSingleton<IRuntimeContext, RuntimeContext>();
        _ = services.AddSingleton(sp => sp.GetRequiredService<IRuntimeContext>() as IDisplayEnvironmentDiagnostic
            ?? throw new InvalidOperationException(
                $"The registered {nameof(IRuntimeContext)} must also implement {nameof(IDisplayEnvironmentDiagnostic)}."));
        services.TryAddSingleton<IRuntimeLogLevelService, RuntimeLogLevelService>();
    }

    private static void ConfigureInitialLogging(CliParseResult parseResult)
    {
        if (parseResult.Options is McpCliOptions)
        {
            var logLevel = string.IsNullOrWhiteSpace(parseResult.Options.LogLevel) ? "Warning" : parseResult.Options.LogLevel;
            LoggerSetup.Initialize(logLevel, enableFileLogging: true, enableConsoleLogging: true, writeConsoleToStandardError: true);
            return;
        }

        var json = parseResult.PrefersJsonOutput;
        LoggerSetup.Initialize(json ? "Fatal" : SettingsService.TryLoadLogLevelEarly(), !json, !json);
    }

    private static void ConfigureCommandLogging(CliCommandOptions options)
    {
        if (options is McpCliOptions)
        {
            return;
        }

        var logLevel = "Fatal";
        if (!options.JsonOutput)
        {
            logLevel = string.IsNullOrWhiteSpace(options.LogLevel) ? "Warning" : options.LogLevel;
        }

        LoggerSetup.SetLogLevel(logLevel);
    }
}
