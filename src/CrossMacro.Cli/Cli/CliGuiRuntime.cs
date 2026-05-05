using System;
using System.Threading.Tasks;
using CrossMacro.Infrastructure.Logging;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
using Serilog;

namespace CrossMacro.Cli;

/// <summary>
/// Shared bootstrap flow for host executables that can run both CLI and GUI modes.
/// </summary>
public static class CliGuiRuntime
{
    private const string DefaultCliLogLevel = "Warning";

    public static async Task<int> RunAsync(
        string[] args,
        IPlatformServiceRegistrar platformServiceRegistrar,
        Func<int> startGui,
        Func<string> getVersionString,
        Func<IDisposable?> tryAcquireSingleInstanceGuard)
    {
        ArgumentNullException.ThrowIfNull(platformServiceRegistrar);
        ArgumentNullException.ThrowIfNull(startGui);
        ArgumentNullException.ThrowIfNull(getVersionString);
        ArgumentNullException.ThrowIfNull(tryAcquireSingleInstanceGuard);

        try
        {
            var commandRouter = new CliCommandRouter();
            var parseResult = commandRouter.Parse(args);
            var logLevel = parseResult.PrefersJsonOutput ? "Fatal" : SettingsService.TryLoadLogLevelEarly();
            LoggerSetup.Initialize(
                logLevel,
                enableFileLogging: !parseResult.PrefersJsonOutput,
                enableConsoleLogging: !parseResult.PrefersJsonOutput);

            switch (parseResult.Kind)
            {
                case CliParseResult.ParseResultKind.Gui:
                    return RunGuiMode(tryAcquireSingleInstanceGuard, startGui);
                case CliParseResult.ParseResultKind.Help:
                    Console.WriteLine(commandRouter.GetUsage(parseResult.HelpTopic));
                    return (int)CliExitCode.Success;
                case CliParseResult.ParseResultKind.Version:
                    Console.WriteLine(getVersionString());
                    return (int)CliExitCode.Success;
                case CliParseResult.ParseResultKind.Error:
                    return WriteParseFailure(commandRouter, parseResult);
                case CliParseResult.ParseResultKind.Success:
                    if (parseResult.Options == null)
                    {
                        throw new InvalidOperationException("Successful CLI parse result must include command options.");
                    }

                    ConfigureCliLogging(parseResult.Options);
                    return await RunCliModeAsync(platformServiceRegistrar, parseResult.Options, tryAcquireSingleInstanceGuard).ConfigureAwait(false);
                default:
                    throw new InvalidOperationException($"Unsupported CLI parse result kind: {parseResult.Kind}");
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static bool RequiresSingleInstanceGuard(CliCommandOptions options)
    {
        return options is HeadlessCliOptions;
    }

    private static int RunGuiMode(Func<IDisposable?> tryAcquireSingleInstanceGuard, Func<int> startGui)
    {
        using var guiInstanceGuard = tryAcquireSingleInstanceGuard();
        if (guiInstanceGuard == null)
        {
            Log.Warning("Could not acquire single-instance lock; another instance may already be running.");
            return (int)CliExitCode.EnvironmentError;
        }

        return startGui();
    }

    private static int WriteParseFailure(CliCommandRouter commandRouter, CliParseResult parseResult)
    {
        var message = parseResult.ErrorMessage ?? "Invalid command line arguments.";
        var errorDetails = parseResult.ErrorDetails.Count > 0
            ? parseResult.ErrorDetails
            : ["See crossmacro --help for usage information."];

        if (parseResult.PrefersJsonOutput)
        {
            var parseError = CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                message,
                errors: errorDetails);
            CliOutputFormatter.Write(parseError, jsonOutput: true);
        }
        else
        {
            Console.Error.WriteLine(message);
            Console.Error.WriteLine();

            if (parseResult.ShowTopLevelUsageInTextMode)
            {
                Console.Error.WriteLine(commandRouter.GetUsage());
            }
            else if (parseResult.ErrorDetails.Count > 0)
            {
                foreach (var detail in parseResult.ErrorDetails)
                {
                    Console.Error.WriteLine(detail);
                }
            }
            else
            {
                Console.Error.WriteLine(commandRouter.GetUsage());
            }
        }

        return (int)CliExitCode.InvalidArguments;
    }

    private static void ConfigureCliLogging(CliCommandOptions options)
    {
        if (options.JsonOutput)
        {
            // Keep stdout clean for machine-readable JSON output.
            LoggerSetup.SetLogLevel("Fatal");
        }
        else if (!string.IsNullOrWhiteSpace(options.LogLevel))
        {
            LoggerSetup.SetLogLevel(options.LogLevel);
        }
        else
        {
            // Keep CLI output focused unless user explicitly opts into a lower level.
            LoggerSetup.SetLogLevel(DefaultCliLogLevel);
        }
    }

    private static async Task<int> RunCliModeAsync(
        IPlatformServiceRegistrar platformServiceRegistrar,
        CliCommandOptions options,
        Func<IDisposable?> tryAcquireSingleInstanceGuard)
    {
        if (!RequiresSingleInstanceGuard(options))
        {
            return await new CliHost(platformServiceRegistrar).RunAsync(options).ConfigureAwait(false);
        }

        using var cliInstanceGuard = tryAcquireSingleInstanceGuard();
        if (cliInstanceGuard == null)
        {
            var conflictResult = CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Another CrossMacro runtime instance is already running.",
                errors:
                [
                    "Headless mode cannot start while another GUI/headless runtime holds the single-instance lock."
                ]);
            CliOutputFormatter.Write(conflictResult, options.JsonOutput);
            return (int)CliExitCode.EnvironmentError;
        }

        return await new CliHost(platformServiceRegistrar).RunAsync(options).ConfigureAwait(false);
    }
}
