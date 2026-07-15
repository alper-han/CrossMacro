using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Cli.DependencyInjection;
using CrossMacro.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace CrossMacro.Cli;

public sealed class CliHost
{
    private readonly Action<IServiceCollection> _configureServices;
    private readonly Action<IServiceCollection, CliRuntimeProfile> _configureRuntimeServices;
    private readonly Action<CliCommandOptions> _configureHostLogging;

    public CliHost(Action<IServiceCollection> configureServices)
        : this(configureServices, static (_, _) => { }, static _ => { })
    {
    }

    public CliHost(
        Action<IServiceCollection> configureServices,
        Action<IServiceCollection, CliRuntimeProfile> configureRuntimeServices,
        Action<CliCommandOptions>? configureHostLogging = null)
    {
        _configureServices = configureServices ?? throw new ArgumentNullException(nameof(configureServices));
        _configureRuntimeServices = configureRuntimeServices ?? throw new ArgumentNullException(nameof(configureRuntimeServices));
        _configureHostLogging = configureHostLogging ?? (static _ => { });
    }

    [Obsolete("Use executable composition callbacks.")]
    public CliHost(IPlatformServiceRegistrar platformServiceRegistrar)
        : this(services =>
        {
            platformServiceRegistrar.RegisterPlatformServices(services);
        })
    {
        ArgumentNullException.ThrowIfNull(platformServiceRegistrar);
    }

    public async Task<int> RunAsync(CliCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            _configureHostLogging(options);

            var services = new ServiceCollection();
            var runtimeProfile = GetRuntimeProfile(options);
            _configureServices(services);
            _configureRuntimeServices(services, runtimeProfile);
            services.AddCliServices();

            await using var provider = services.BuildServiceProvider();
            if (RequiresProfileInitialization(options))
            {
                await InitializeProfilesAsync(provider).ConfigureAwait(false);
            }
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
                    ex.Message,
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

    private static bool RequiresProfileInitialization(CliCommandOptions options)
    {
        return options is SettingsGetCliOptions
            or SettingsSetCliOptions
            or SettingsListKeysCliOptions
            or SettingsResetCliOptions
            or ProfileCliOptions
            or TextExpansionCliOptions
            or ShortcutListCliOptions
            or ShortcutRunCliOptions
            or ShortcutCliOptions
            or ScheduleListCliOptions
            or ScheduleRunCliOptions
            or ScheduleCliOptions
            or TriggerListCliOptions
            or TriggerCliOptions
            or HeadlessCliOptions;
    }
}
