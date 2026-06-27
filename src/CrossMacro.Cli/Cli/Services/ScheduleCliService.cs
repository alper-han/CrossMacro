using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Cli.Serialization;

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
            mapTaskResult: task => new ScheduleTaskRunData(
                task.Id,
                task.Name,
                task.IsEnabled,
                task.MacroFilePath,
                task.LastRunTime,
                task.LastStatus
            ));
    }

    private static ScheduleTaskData MapScheduleTask(ScheduledTask task)
    {
        if (task.Type == ScheduleType.Weekly)
        {
            return new ScheduleTaskData(
                task.Id,
                task.Name,
                task.IsEnabled,
                task.Type.ToString(),
                task.MacroFilePath,
                task.WeeklyDays.ToString(),
                task.WeeklyTime.ToString(),
                task.NextRunTime,
                task.LastRunTime,
                task.LastStatus
            );
        }

        return new ScheduleTaskData(
            task.Id,
            task.Name,
            task.IsEnabled,
            task.Type.ToString(),
            task.MacroFilePath,
            null,
            null,
            task.NextRunTime,
            task.LastRunTime,
            task.LastStatus
        );
    }
}
