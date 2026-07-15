
namespace CrossMacro.Cli.Commands;

public sealed class ScheduleCommandHandler : CliCommandHandlerBase<ScheduleCliOptions>
{
    private readonly IScheduleCliService _scheduleCliService;

    public ScheduleCommandHandler(IScheduleCliService scheduleCliService)
    {
        _scheduleCliService = scheduleCliService;
    }

    protected override Task<CliCommandExecutionResult> ExecuteAsync(ScheduleCliOptions options, CancellationToken cancellationToken)
    {
        return _scheduleCliService.ExecuteAsync(options, cancellationToken);
    }
}
