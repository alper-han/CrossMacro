using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

public sealed class ScheduleCliService : IScheduleCliService
{
    private readonly ISchedulerService _schedulerService;

    public ScheduleCliService(ISchedulerService schedulerService)
    {
        _schedulerService = schedulerService;
    }

    public async Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.ListTasksAsync(
            taskKind: "schedule",
            cancellationToken: cancellationToken,
            loadAsync: () => _schedulerService.LoadAsync(),
            getTasks: () => _schedulerService.Tasks,
            mapTask: x => new
            {
                id = x.Id,
                name = x.Name,
                enabled = x.IsEnabled,
                type = x.Type.ToString(),
                macroFilePath = x.MacroFilePath,
                nextRunTime = x.NextRunTime,
                lastRunTime = x.LastRunTime,
                lastStatus = x.LastStatus
            });
    }

    public async Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.RunTaskAsync(
            taskId: taskId,
            taskKindLower: "schedule",
            taskKindDisplay: "Schedule",
            cancellationToken: cancellationToken,
            loadAsync: () => _schedulerService.LoadAsync(),
            getTasks: () => _schedulerService.Tasks,
            getTaskId: x => x.Id,
            runTaskAsync: (parsedTaskId, cancellationToken) => _schedulerService.RunTaskAsync(parsedTaskId, cancellationToken),
            mapTaskResult: task => new
            {
                id = task.Id,
                name = task.Name,
                enabled = task.IsEnabled,
                macroFilePath = task.MacroFilePath,
                lastRunTime = task.LastRunTime,
                lastStatus = task.LastStatus
            });
    }
}
