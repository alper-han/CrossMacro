
namespace CrossMacro.Cli.Commands;

public sealed class ScheduleCommandHandler(IScheduleCliService scheduleCliService) : CliCommandHandlerBase<ScheduleCliOptions>
{
    private readonly IScheduleCliService _scheduleCliService = scheduleCliService ?? throw new ArgumentNullException(nameof(scheduleCliService));

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ScheduleCliOptions options, CancellationToken cancellationToken)
    {
        return await _scheduleCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
    }
}
