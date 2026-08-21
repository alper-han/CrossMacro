namespace CrossMacro.UI.Hosting;

/// <summary>
/// Bootstrap pieces shared verbatim by the three GUI launchers (Linux/Windows/macOS).
/// Compiled into each launcher via a linked Compile item: no existing project can host it
/// because it spans UI, Cli and Infrastructure types at once.
/// </summary>
internal static class GuiHostBootstrap
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

    /// <summary>Registers the diagnostics that hang off an already-registered IRuntimeContext.</summary>
    public static void AddRuntimeDiagnostics(IServiceCollection services)
    {
        _ = services.AddSingleton<IDisplayEnvironmentDiagnostic>(sp =>
            (IDisplayEnvironmentDiagnostic)sp.GetRequiredService<IRuntimeContext>());
        _ = services.AddSingleton<IRuntimeLogLevelService, RuntimeLogLevelService>();
    }

    /// <summary>GUI services registered identically on every platform.</summary>
    public static void AddCommonGuiServices(IServiceCollection services)
    {
        _ = services.AddSingleton<AvaloniaClipboardService>();
        _ = services.AddSingleton<IUpdateService, GitHubUpdateService>();
        _ = services.AddSingleton<Func<CancellationToken, Task>>(sp =>
            token => sp.GetRequiredService<IScreenReadingWarmupService>().WarmUpPortalSessionAsync(token));
    }
}
