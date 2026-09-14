namespace CrossMacro.Cli.Services.Automation;

/// <summary>Maps CLI syntax and output around the shared application task commands.</summary>
public sealed class TriggerCliService(ITriggerCommands commands) : ITriggerCliService
{
    private readonly ITriggerCommands _commands = commands ?? throw new ArgumentNullException(nameof(commands));

    public TriggerCliService(IManageTrigger workflow) : this(new TriggerCommands(workflow)) { }


    public async Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken) =>
        TaskCliResultMapper.FromApplication(await _commands.ListAsync(cancellationToken).ConfigureAwait(false));

    public async Task<CliCommandExecutionResult> ExecuteAsync(TriggerCliOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return TaskCliResultMapper.FromApplication(await _commands.ExecuteAsync(TaskCommandOptionsMapper.ToApplication(options), cancellationToken).ConfigureAwait(false));
    }
}
