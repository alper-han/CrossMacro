namespace CrossMacro.Cli.Commands;

public sealed class ScheduleCommandHandler(IScheduleCommands commands) : CliCommandHandlerBase<ScheduleCliOptions>
{
    private readonly IScheduleCommands _commands = commands ?? throw new ArgumentNullException(nameof(commands));

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ScheduleCliOptions options, CancellationToken cancellationToken)
    {
        return TaskCliResultMapper.FromApplication(await _commands.ExecuteAsync(TaskCommandOptionsMapper.ToApplication(options), cancellationToken).ConfigureAwait(false));
    }
}
