namespace CrossMacro.Application.Automation;

public interface IScheduleCommands
{
    public Task<TaskCommandResult<ScheduledTask>> ListAsync(CancellationToken cancellationToken);
    public Task<TaskCommandResult<ScheduledTask>> RunAsync(string taskId, CancellationToken cancellationToken);
    public Task<TaskCommandResult<ScheduledTask>> ExecuteAsync(ScheduleCommand options, CancellationToken cancellationToken);
}
