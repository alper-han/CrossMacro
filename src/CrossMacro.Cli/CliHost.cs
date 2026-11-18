using System.Runtime.CompilerServices;
using CrossMacro.Cli.DependencyInjection;
using CrossMacro.Core.Services.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Cli;

public sealed class CliHost(Action<IServiceCollection, CliRuntimeProfile> configureRuntimeServices)
{
    private readonly Action<IServiceCollection, CliRuntimeProfile> _configureRuntimeServices = configureRuntimeServices ?? throw new ArgumentNullException(nameof(configureRuntimeServices));

    public async Task<int> RunAsync(CommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var services = new ServiceCollection();
            var runtimeProfile = GetRuntimeProfile(options);
            _configureRuntimeServices(services, runtimeProfile);
            _ = services.AddCliServices();

            var provider = services.BuildServiceProvider();
            await using var providerDisposal = provider.ConfigureAwait(false);
            if (options is ProfileBackedOptions)
            {
                await InitializeProfilesAsync(provider).ConfigureAwait(false);
            }
            var commandExecutor = provider.GetRequiredService<CliCommandExecutor>();

            using var cancellation = new CancellationTokenSource();
            void cancelHandler(object? _, ConsoleCancelEventArgs eventArgs)
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            }

            Console.CancelKeyPress += cancelHandler;
            try
            {
                if (options is McpOptions)
                {
                    return (int)(await commandExecutor.ExecuteResultAsync(options, cancellation.Token).ConfigureAwait(false)).Outcome;
                }

                return await commandExecutor.ExecuteAsync(options, cancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (OperationCanceledException)
        {
            return await WriteCancelledAsync(options).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SerilogLog.Error(ex, "CLI command failed");

            var runtimeFailure = OperationResult.Fail(
                ExecutionOutcomeCode.RuntimeError,
                "CLI command failed.",
                errors:
                [
                    ex.Message,
                ]);
            await WriteResultAsync(options, runtimeFailure).ConfigureAwait(false);
            return (int)ExecutionOutcomeCode.RuntimeError;
        }
    }

    private static CliRuntimeProfile GetRuntimeProfile(CommandOptions options)
    {
        return options is HeadlessOptions
            or McpOptions
            ? CliRuntimeProfile.Persistent
            : CliRuntimeProfile.OneShot;
    }

    private static async Task InitializeProfilesAsync(IServiceProvider provider)
    {
        await provider.GetRequiredService<IProfileManager>()
            .InitializeAsync()
            .ConfigureAwait(false);
    }

    private static async Task<int> WriteCancelledAsync(CommandOptions options)
    {
        await WriteResultAsync(options, OperationResult.Fail(ExecutionOutcomeCode.Cancelled, "Command cancelled.")).ConfigureAwait(false);
        return (int)ExecutionOutcomeCode.Cancelled;
    }

    private static async ValueTask WriteResultAsync(CommandOptions options, OperationResult result)
    {
        if (options is McpOptions)
        {
            await Console.Error.WriteLineAsync(result.Message.AsMemory(), CancellationToken.None).ConfigureAwait(false);
            foreach (var error in result.Errors)
            {
                await Console.Error.WriteLineAsync(error.AsMemory(), CancellationToken.None).ConfigureAwait(false);
            }

            return;
        }

        CliOutputFormatter.Write(result, options.JsonOutput);
    }
}
