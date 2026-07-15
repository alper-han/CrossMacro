
namespace CrossMacro.Application.Automation;

public interface IManageSchedule
{
    Task<TaskCollectionResult<ScheduledTask>> ListAsync(CancellationToken cancellationToken = default);
    Task<ScheduledTask> AddAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    Task<ScheduledTask> UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    Task<ScheduledTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default);
    Task<ScheduledTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default);
    Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default);
}
