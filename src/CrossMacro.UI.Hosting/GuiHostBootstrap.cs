using CrossMacro.Cli;
using CrossMacro.Cli.Options;
using CrossMacro.Core.Services.Runtime;
using CrossMacro.Core.Services.Updates;
using CrossMacro.Infrastructure.DependencyInjection;
using CrossMacro.Infrastructure.Logging;
using CrossMacro.Infrastructure.Services.Settings;
using CrossMacro.Infrastructure.Services.Updates;
using CrossMacro.Platform.Abstractions.Input.Simulation;
using CrossMacro.Platform.Abstractions.Runtime;
using CrossMacro.Platform.Abstractions.ScreenReading;
using CrossMacro.UI.Services.Clipboard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrossMacro.UI.Hosting;

/// <summary>
/// Shared composition root pieces for the Linux, Windows, and macOS GUI hosts.
/// This project intentionally owns the otherwise cross-cutting UI, CLI, and
/// infrastructure registrations so each executable stays platform-specific.
/// </summary>
public static class GuiHostBootstrap
{
    public static CliBootstrapCallbacks CreateBootstrapCallbacks() =>
        new(ConfigureInitialLogging, ConfigureCommandLogging, ConfigureHostLogging);

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

    private static void ConfigureHostLogging(CliCommandOptions options)
    {
        if (options is McpCliOptions)
        {
            var logLevel = string.IsNullOrWhiteSpace(options.LogLevel) ? "Warning" : options.LogLevel;
            LoggerSetup.Initialize(logLevel, enableFileLogging: true, enableConsoleLogging: true, writeConsoleToStandardError: true);
            return;
        }

        if (options.JsonOutput)
        {
            LoggerSetup.Initialize("Fatal", enableFileLogging: false, enableConsoleLogging: false);
        }
    }

    public static void ConfigureGuiRuntimeServices(IServiceCollection services)
    {
        _ = services.AddCrossMacroCommonRuntimeServices();
        _ = services.AddCrossMacroSharedPostPlatformRuntimeServices(sp => sp.GetService<IInputSimulatorPool>());
    }

    /// <summary>Registers diagnostics that hang off an already-registered <see cref="IRuntimeContext"/>.</summary>
    public static void AddRuntimeDiagnostics(IServiceCollection services)
    {
        _ = services.AddSingleton<IDisplayEnvironmentDiagnostic>(sp =>
            (IDisplayEnvironmentDiagnostic)sp.GetRequiredService<IRuntimeContext>());
        services.TryAddSingleton<IRuntimeLogLevelService, RuntimeLogLevelService>();
    }

    /// <summary>Registers GUI services that are identical on every desktop platform.</summary>
    public static void AddCommonGuiServices(IServiceCollection services)
    {
        _ = services.AddSingleton<AvaloniaClipboardService>();
        _ = services.AddSingleton<IUpdateService, GitHubUpdateService>();
        _ = services.AddSingleton<Func<CancellationToken, Task>>(sp =>
            token => sp.GetRequiredService<IScreenReadingWarmupService>().WarmUpPortalSessionAsync(token));
    }
}
