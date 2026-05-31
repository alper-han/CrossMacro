using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
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
            mapTask: MapScheduleTask);
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

    private static object MapScheduleTask(ScheduledTask task)
    {
        if (task.Type == ScheduleType.Weekly)
        {
            return new
            {
                id = task.Id,
                name = task.Name,
                enabled = task.IsEnabled,
                type = task.Type.ToString(),
                macroFilePath = task.MacroFilePath,
                weeklyDays = task.WeeklyDays.ToString(),
                weeklyTime = task.WeeklyTime,
                nextRunTime = task.NextRunTime,
                lastRunTime = task.LastRunTime,
                lastStatus = task.LastStatus
            };
        }

        return new
        {
            id = task.Id,
            name = task.Name,
            enabled = task.IsEnabled,
            type = task.Type.ToString(),
            macroFilePath = task.MacroFilePath,
            nextRunTime = task.NextRunTime,
            lastRunTime = task.LastRunTime,
            lastStatus = task.LastStatus
        };
    }
}
