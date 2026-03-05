using System;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Logging;
using CrossMacro.Infrastructure.Services;
using Serilog;

namespace CrossMacro.Cli;

/// <summary>
/// Shared bootstrap flow for host executables that can run both CLI and GUI modes.
/// </summary>
public static class CliGuiRuntime
{
    private const string DefaultCliLogLevel = "Warning";

    public static int Run(
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

        var logLevel = SettingsService.TryLoadLogLevelEarly();
        LoggerSetup.Initialize(logLevel);

        try
        {
            var commandRouter = new CliCommandRouter();
            var parseResult = commandRouter.Parse(args);

            if (!parseResult.ShouldStartGui)
            {
                if (parseResult.ShowHelp)
                {
                    Console.WriteLine(commandRouter.GetUsage(parseResult.HelpTopic));
                    return (int)CliExitCode.Success;
                }

                if (parseResult.ShowVersion)
                {
                    Console.WriteLine(getVersionString());
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
                        CliOutputFormatter.Write(conflictResult, parseResult.Options.JsonOutput);
                        return (int)CliExitCode.EnvironmentError;
                    }
                }

                return new CliHost(platformServiceRegistrar).Run(parseResult.Options);
            }

            using var guiInstanceGuard = tryAcquireSingleInstanceGuard();
            if (guiInstanceGuard == null)
            {
                Log.Warning("Could not acquire single-instance lock; another instance may already be running.");
                return (int)CliExitCode.EnvironmentError;
            }

            return startGui();
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
}
