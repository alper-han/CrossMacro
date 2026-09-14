namespace CrossMacro.Cli.Commands;

public sealed class ScheduleListCommandHandler(IScheduleCommands commands) : CliCommandHandlerBase<ScheduleListCliOptions>
{
    private readonly IScheduleCommands _commands = commands ?? throw new ArgumentNullException(nameof(commands));

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ScheduleListCliOptions options, CancellationToken cancellationToken)
    {
        return TaskCliResultMapper.FromApplication(await _commands.ListAsync(cancellationToken).ConfigureAwait(false));
    }
}
