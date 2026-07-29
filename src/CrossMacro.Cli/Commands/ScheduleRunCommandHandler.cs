
namespace CrossMacro.Cli.Commands;

public sealed class ScheduleRunCommandHandler(IScheduleCliService scheduleCliService) : CliCommandHandlerBase<ScheduleRunCliOptions>
{
    private readonly IScheduleCliService _scheduleCliService = scheduleCliService;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ScheduleRunCliOptions options, CancellationToken cancellationToken)
    {
        return await _scheduleCliService.RunAsync(options.TaskId, cancellationToken).ConfigureAwait(false);
    }
}
