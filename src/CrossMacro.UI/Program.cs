using Avalonia;
using Avalonia.Media;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Cli;
using Serilog;
using System;
using System.Reflection;

namespace CrossMacro.UI;

public static class Program
{
    private const string SingleInstanceName = "CrossMacro.UI.SingleInstance";
    private const string DefaultCliLogLevel = "Warning";

    public static int Run(
        string[] args,
        IPlatformServiceRegistrar platformServiceRegistrar,
        Action<AppBuilder, string[]> startApplication)
    {
        ArgumentNullException.ThrowIfNull(platformServiceRegistrar);
        ArgumentNullException.ThrowIfNull(startApplication);

        // Load log level from settings before logger initialization.
        var logLevel = SettingsService.TryLoadLogLevelEarly();

        // Initialize logger with user's preferred level.
        LoggerSetup.Initialize(logLevel);

        var commandRouter = new CliCommandRouter();
        var parseResult = commandRouter.Parse(args);

        if (!parseResult.ShouldStartGui)
        {
            if (parseResult.ShowHelp)
            {
                Console.WriteLine(commandRouter.GetUsage(parseResult.HelpTopic));
                Log.CloseAndFlush();
                return (int)CliExitCode.Success;
            }

            if (parseResult.ShowVersion)
            {
                Console.WriteLine(GetVersionString());
                Log.CloseAndFlush();
                return (int)CliExitCode.Success;
            }

            if (!parseResult.IsSuccess || parseResult.Options == null)
            {
                if (WantsJsonOutput(args))
                {
                    var parseError = CliCommandExecutionResult.Fail(
                        CliExitCode.InvalidArguments,
                        parseResult.ErrorMessage ?? "Invalid command line arguments.",
                        errors:
                        [
                            "See --help for usage information."
                        ]);
                    CliOutputFormatter.Write(parseError, jsonOutput: true);
                }
                else
                {
                    Console.Error.WriteLine(parseResult.ErrorMessage ?? "Invalid command line arguments.");
                    Console.Error.WriteLine();
                    Console.Error.WriteLine(commandRouter.GetUsage());
                }

                Log.CloseAndFlush();
                return (int)CliExitCode.InvalidArguments;
            }

            if (parseResult.Options.JsonOutput)
            {
                // Keep stdout clean for machine-readable JSON output.
                LoggerSetup.SetLogLevel("Fatal");
            }
            else if (!string.IsNullOrWhiteSpace(parseResult.Options.LogLevel))
            {
                LoggerSetup.SetLogLevel(parseResult.Options.LogLevel);
            }
            else
            {
                // Keep CLI output focused unless user explicitly opts into a lower level.
                LoggerSetup.SetLogLevel(DefaultCliLogLevel);
            }

            if (RequiresSingleInstanceGuard(parseResult.Options))
            {
                using var cliInstanceGuard = SingleInstanceGuard.TryAcquire(GetSingleInstanceMutexName());
                if (cliInstanceGuard == null)
                {
                    var conflictResult = CliCommandExecutionResult.Fail(
                        CliExitCode.EnvironmentError,
                        "Another CrossMacro runtime instance is already running.",
                        errors:
                        [
                            "Headless mode cannot start while another GUI/headless runtime holds the single-instance lock."
                        ]);
                    CliOutputFormatter.Write(conflictResult, parseResult.Options.JsonOutput);
                    Log.CloseAndFlush();
                    return (int)CliExitCode.EnvironmentError;
                }
            }

            var cliExitCode = new CliHost(platformServiceRegistrar).Run(parseResult.Options);
            Log.CloseAndFlush();
            return cliExitCode;
        }

        SingleInstanceGuard? instanceGuard = null;

        try
        {
            instanceGuard = SingleInstanceGuard.TryAcquire(GetSingleInstanceMutexName());
            if (instanceGuard == null)
            {
                Log.Warning("Could not acquire single-instance lock; another instance may already be running.");
                return 0;
            }

            App.PlatformServiceRegistrar = platformServiceRegistrar;
            Log.Information("Starting CrossMacro application");
            startApplication(BuildAvaloniaApp(), args);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            instanceGuard?.Dispose();
            App.PlatformServiceRegistrar = null;
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .WithInterFont()
            .LogToTrace()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://Avalonia.Fonts.Inter/Assets#Inter",
                FontFallbacks =
                [
                    new FontFallback { FontFamily = new FontFamily("avares://Avalonia.Fonts.Inter/Assets#Inter") }
                ]
            });

    private static string GetSingleInstanceMutexName()
    {
        // Windows without "Global\" is session scoped; we want system-wide single instance.
        return OperatingSystem.IsWindows() ? $@"Global\{SingleInstanceName}" : SingleInstanceName;
    }

    private static bool RequiresSingleInstanceGuard(CliCommandOptions options)
    {
        return options is HeadlessCliOptions;
    }

    private static bool WantsJsonOutput(string[] args)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetVersionString()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var name = assembly.GetName();
        var version = name.Version;
        if (version == null)
        {
            return name.Name ?? "CrossMacro";
        }

        return $"{name.Name} {version.Major}.{version.Minor}.{version.Build}";
    }
}
