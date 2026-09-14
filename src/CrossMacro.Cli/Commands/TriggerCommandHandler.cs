namespace CrossMacro.Cli.Commands;

public sealed class TriggerCommandHandler(ITriggerCommands commands) : CliCommandHandlerBase<TriggerCliOptions>
{
    private readonly ITriggerCommands _commands = commands ?? throw new ArgumentNullException(nameof(commands));

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(TriggerCliOptions options, CancellationToken cancellationToken)
    {
        return TaskCliResultMapper.FromApplication(await _commands.ExecuteAsync(TaskCommandOptionsMapper.ToApplication(options), cancellationToken).ConfigureAwait(false));
    }
}
