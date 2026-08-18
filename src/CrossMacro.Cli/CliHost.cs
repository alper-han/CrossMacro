
namespace CrossMacro.Cli;

public sealed class CliHost(
    Action<IServiceCollection> configureServices,
    Action<IServiceCollection, CliRuntimeProfile> configureRuntimeServices,
    Action<CliCommandOptions>? configureHostLogging = null)
{
    private readonly Action<IServiceCollection> _configureServices = configureServices ?? throw new ArgumentNullException(nameof(configureServices));
    private readonly Action<IServiceCollection, CliRuntimeProfile> _configureRuntimeServices = configureRuntimeServices ?? throw new ArgumentNullException(nameof(configureRuntimeServices));
    private readonly Action<CliCommandOptions> _configureHostLogging = configureHostLogging ?? (static _ => { });

    public CliHost(Action<IServiceCollection> configureServices)
        : this(configureServices, static (_, _) => { }, static _ => { }) { /* Empty */ }

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
            _ = services.AddCliServices();

            var provider = services.BuildServiceProvider();
            await using var providerDisposal = provider.ConfigureAwait(false);
            if (RequiresProfileInitialization(options))
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SerilogLog.Error(ex, "CLI command failed");

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
