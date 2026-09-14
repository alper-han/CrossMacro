using CrossMacro.Application.Automation;
namespace CrossMacro.Mcp.Tests;

internal sealed class ScheduleCommandTestAdapter(IScheduleCliService service) : IScheduleCommands
{
    public async Task<TaskCommandResult<ScheduledTask>> ListAsync(CancellationToken cancellationToken) =>
        TaskCommandTestResults.Schedule(await service.ListAsync(cancellationToken));
    public async Task<TaskCommandResult<ScheduledTask>> RunAsync(string taskId, CancellationToken cancellationToken) =>
        TaskCommandTestResults.Schedule(await service.RunAsync(taskId, cancellationToken));
    public async Task<TaskCommandResult<ScheduledTask>> ExecuteAsync(ScheduleCommand options, CancellationToken cancellationToken) =>
        TaskCommandTestResults.Schedule(await service.ExecuteAsync(new ScheduleCliOptions(Enum.Parse<ScheduleCliAction>(options.Action.ToString()), options.TaskId, options.Name, options.MacroFilePath, options.Interval, options.At, options.Weekly, options.Time, options.Speed, options.Enabled), cancellationToken));
}
