
namespace CrossMacro.Application.Automation;

public interface IManageSchedule
{
    public Task<TaskCollectionResult<ScheduledTask>> ListAsync(CancellationToken cancellationToken = default);
    public Task<ScheduledTask> AddAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    public Task<ScheduledTask> UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    public Task<ScheduledTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default);
    public Task<ScheduledTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default);
    public Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default);
}
