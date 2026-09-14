namespace CrossMacro.Cli.Commands;

public sealed class ScheduleRunCommandHandler(IScheduleCommands commands) : CliCommandHandlerBase<ScheduleRunCliOptions>
{
    private readonly IScheduleCommands _commands = commands ?? throw new ArgumentNullException(nameof(commands));

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ScheduleRunCliOptions options, CancellationToken cancellationToken)
    {
        return TaskCliResultMapper.FromApplication(await _commands.RunAsync(options.TaskId, cancellationToken).ConfigureAwait(false));
    }
}
