using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.DependencyInjection;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Logging;
using CrossMacro.Platform.Abstractions;
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

    public async Task<int> RunAsync(CliCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            ConfigureDirectHostLogging(options);

            var services = new ServiceCollection();
            var runtimeProfile = GetRuntimeProfile(options);
            services.AddCrossMacroCliRuntimeServices(_platformServiceRegistrar, runtimeProfile);
            services.AddCliServices();

            await using var provider = services.BuildServiceProvider();
            await InitializeProfilesAsync(provider).ConfigureAwait(false);
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
                return await commandExecutor.ExecuteAsync(options, cancellation.Token).ConfigureAwait(false);
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

    private static CliRuntimeProfile GetRuntimeProfile(CliCommandOptions options)
    {
        return options is HeadlessCliOptions
            ? CliRuntimeProfile.Persistent
            : CliRuntimeProfile.OneShot;
    }

    private static async Task InitializeProfilesAsync(IServiceProvider provider)
    {
        await provider.GetRequiredService<IProfileManager>()
            .InitializeAsync()
            .ConfigureAwait(false);
    }

    private static void ConfigureDirectHostLogging(CliCommandOptions options)
    {
        if (options.JsonOutput)
        {
            LoggerSetup.Initialize("Fatal", enableFileLogging: false, enableConsoleLogging: false);
        }
    }
}
