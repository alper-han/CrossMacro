using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Cli;

/// <summary>
/// Shared bootstrap flow for host executables that can run both CLI and GUI modes.
/// </summary>
public static class CliGuiRuntime
{
    public static async Task<int> RunAsync(
        ReadOnlyMemory<string> args,
        Action<IServiceCollection, CliRuntimeProfile> configureCliServices,
        Func<int> startGui,
        Func<string> getVersionString,
        Func<IDisposable?> tryAcquireSingleInstanceGuard,
        CliBootstrapCallbacks? bootstrapCallbacks = null,
        Func<bool>? tryActivateExistingInstance = null)
    {
        ArgumentNullException.ThrowIfNull(configureCliServices);
        ArgumentNullException.ThrowIfNull(startGui);
        ArgumentNullException.ThrowIfNull(getVersionString);
        ArgumentNullException.ThrowIfNull(tryAcquireSingleInstanceGuard);
        bootstrapCallbacks ??= CliBootstrapCallbacks.NoOp;

        try
        {
            var parseResult = CliCommandRouter.Parse(args);
            bootstrapCallbacks.ConfigureInitialLogging(parseResult);

            switch (parseResult.Kind)
            {
                case CliParseResult.ParseResultKind.Gui:
                    return RunGuiMode(tryAcquireSingleInstanceGuard, startGui, tryActivateExistingInstance);
                case CliParseResult.ParseResultKind.Help:
                    Console.WriteLine(CliCommandRouter.GetUsage(parseResult.HelpTopic));
                    return (int)ExecutionOutcomeCode.Success;
                case CliParseResult.ParseResultKind.Version:
                    Console.WriteLine(getVersionString());
                    return (int)ExecutionOutcomeCode.Success;
                case CliParseResult.ParseResultKind.Error:
                    return WriteParseFailure(parseResult);
                case CliParseResult.ParseResultKind.Success:
                    if (parseResult.Options == null)
                    {
                        throw new InvalidOperationException("Successful CLI parse result must include command options.");
                    }

                    bootstrapCallbacks.ConfigureCommandLogging(parseResult.Options);
                    return await RunCliModeAsync(configureCliServices, parseResult.Options, tryAcquireSingleInstanceGuard).ConfigureAwait(false);
                default:
                    throw new InvalidOperationException($"Unsupported CLI parse result kind: {parseResult.Kind}");
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SerilogLog.Fatal(ex, "Application terminated unexpectedly");
            return (int)ExecutionOutcomeCode.RuntimeError;
        }
        finally
        {
            await SerilogLog.CloseAndFlushAsync().ConfigureAwait(false);
        }
    }

    private static bool RequiresSingleInstanceGuard(CommandOptions options)
    {
        return options is HeadlessOptions;
    }

    private static int RunGuiMode(
        Func<IDisposable?> tryAcquireSingleInstanceGuard,
        Func<int> startGui,
        Func<bool>? tryActivateExistingInstance)
    {
        using var guiInstanceGuard = tryAcquireSingleInstanceGuard();
        if (guiInstanceGuard is null)
        {
            if (tryActivateExistingInstance?.Invoke() is true)
            {
                SerilogLog.Debug("Activated the existing CrossMacro instance");
                return (int)ExecutionOutcomeCode.Success;
            }

            SerilogLog.Warning("Could not acquire single-instance lock; another instance may already be running.");
            return (int)ExecutionOutcomeCode.EnvironmentError;
        }

        try
        {
            return startGui();
        }
        catch (OperationCanceledException ex)
        {
            SerilogLog.Debug(ex, "GUI shutdown canceled an Avalonia background operation.");
            return (int)ExecutionOutcomeCode.Success;
        }
    }

    private static int WriteParseFailure(CliParseResult parseResult)
    {
        var message = parseResult.ErrorMessage ?? "Invalid command line arguments.";
        var errorDetails = parseResult.ErrorDetails.Count > 0
            ? parseResult.ErrorDetails
            : ["See crossmacro --help for usage information."];

        if (parseResult.PrefersJsonOutput)
        {
            var parseError = OperationResult.Fail(
                ExecutionOutcomeCode.InvalidArguments,
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
                Console.Error.WriteLine(CliCommandRouter.GetUsage());
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
                Console.Error.WriteLine(CliCommandRouter.GetUsage());
            }
        }

        return (int)ExecutionOutcomeCode.InvalidArguments;
    }

    private static async Task<int> RunCliModeAsync(
        Action<IServiceCollection, CliRuntimeProfile> configureServices,
        CommandOptions options,
        Func<IDisposable?> tryAcquireSingleInstanceGuard)
    {
        if (!RequiresSingleInstanceGuard(options))
        {
            return await new CliHost(configureServices).RunAsync(options).ConfigureAwait(false);
        }

        using var cliInstanceGuard = tryAcquireSingleInstanceGuard();
        if (cliInstanceGuard is null)
        {
            var conflictResult = OperationResult.Fail(
                ExecutionOutcomeCode.EnvironmentError,
                "Another CrossMacro runtime instance is already running.",
                errors:
                [
                    "Headless runtime mode cannot start while another GUI or headless runtime holds the single-instance lock.",
                ]);
            CliOutputFormatter.Write(conflictResult, options.JsonOutput);
            return (int)ExecutionOutcomeCode.EnvironmentError;
        }

        return await new CliHost(configureServices).RunAsync(options).ConfigureAwait(false);
    }
}
