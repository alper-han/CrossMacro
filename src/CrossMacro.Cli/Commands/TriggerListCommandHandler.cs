namespace CrossMacro.Cli.Commands;

public sealed class TriggerListCommandHandler(ITriggerCommands commands) : CliCommandHandlerBase<TriggerListCliOptions>
{
    private readonly ITriggerCommands _commands = commands ?? throw new ArgumentNullException(nameof(commands));

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(TriggerListCliOptions options, CancellationToken cancellationToken)
    {
        return TaskCliResultMapper.FromApplication(await _commands.ListAsync(cancellationToken).ConfigureAwait(false));
    }
}
