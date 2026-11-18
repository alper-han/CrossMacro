namespace CrossMacro.Application.Automation;

public interface IManageSchedule : IManageAutomationTasks<ScheduledTask>
{
    public Task<ScheduledTask> AddAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    public Task<ScheduledTask> AddAsync(ScheduledTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default);
    public Task<ScheduledTask> UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    public Task<ScheduledTask> UpdateAsync(ScheduledTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default);
    public Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default);
}
