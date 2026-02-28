using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Commands;

public sealed class ScheduleListCommandHandler : CliCommandHandlerBase<ScheduleListCliOptions>
{
    private readonly IScheduleCliService _scheduleCliService;

    public ScheduleListCommandHandler(IScheduleCliService scheduleCliService)
    {
        _scheduleCliService = scheduleCliService;
    }

    protected override async Task<CliCommandExecutionResult> ExecuteAsync(ScheduleListCliOptions options, CancellationToken cancellationToken)
    {
        return await _scheduleCliService.ListAsync(cancellationToken);
    }
}
