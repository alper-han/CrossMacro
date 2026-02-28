using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class ScheduleRunCommandHandler : CliCommandHandlerBase<ScheduleRunCliOptions>
{
    private readonly IScheduleCliService _scheduleCliService;

    public ScheduleRunCommandHandler(IScheduleCliService scheduleCliService)
    {
        _scheduleCliService = scheduleCliService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ScheduleRunCliOptions options, CancellationToken cancellationToken)
    {
        return await _scheduleCliService.RunAsync(options.TaskId, cancellationToken);
    }
}
