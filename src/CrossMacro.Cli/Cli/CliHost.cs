using System;
using System.Threading;
using CrossMacro.Core.Services;
using CrossMacro.Cli.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace CrossMacro.Cli;

public sealed class CliHost
{
    private readonly IPlatformServiceRegistrar _platformServiceRegistrar;

    public CliHost(IPlatformServiceRegistrar platformServiceRegistrar)
    {
        _platformServiceRegistrar = platformServiceRegistrar;
    }

    public int Run(CliCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var services = new ServiceCollection();
            services.AddCrossMacroCliRuntimeServices(_platformServiceRegistrar);
            services.AddCliServices();

            using var provider = services.BuildServiceProvider();
            var commandExecutor = provider.GetRequiredService<CliCommandExecutor>();

            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler? cancelHandler = null;
            cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };

            Console.CancelKeyPress += cancelHandler;
            try
            {
                return commandExecutor.ExecuteAsync(options, cancellation.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                var cancelledResult = CliCommandExecutionResult.Fail(
                    CliExitCode.Cancelled,
                    "Command cancelled.");
                CliOutputFormatter.Write(cancelledResult, options.JsonOutput);
                return (int)CliExitCode.Cancelled;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (OperationCanceledException)
        {
            var cancelledResult = CliCommandExecutionResult.Fail(
                CliExitCode.Cancelled,
                "Command cancelled.");
            CliOutputFormatter.Write(cancelledResult, options.JsonOutput);
            return (int)CliExitCode.Cancelled;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CLI command failed");

            var runtimeFailure = CliCommandExecutionResult.Fail(
                CliExitCode.RuntimeError,
                "CLI command failed.",
                errors:
                [
                    ex.Message
                ]);
            CliOutputFormatter.Write(runtimeFailure, options.JsonOutput);
            return (int)CliExitCode.RuntimeError;
        }
    }
}
