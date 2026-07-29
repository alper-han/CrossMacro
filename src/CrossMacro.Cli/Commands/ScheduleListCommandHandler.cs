
namespace CrossMacro.Cli.Commands;

public sealed class ScheduleListCommandHandler(IScheduleCliService scheduleCliService) : CliCommandHandlerBase<ScheduleListCliOptions>
{
    private readonly IScheduleCliService _scheduleCliService = scheduleCliService;

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ScheduleListCliOptions options, CancellationToken cancellationToken)
    {
        return await _scheduleCliService.ListAsync(cancellationToken).ConfigureAwait(false);
    }
}
