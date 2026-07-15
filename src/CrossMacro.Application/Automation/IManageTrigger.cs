
namespace CrossMacro.Application.Automation;

public interface IManageTrigger
{
    public Task<TaskCollectionResult<TriggerTask>> ListAsync(CancellationToken cancellationToken = default);
    public Task<TriggerTask> AddAsync(TriggerTask task, CancellationToken cancellationToken = default);
    public Task<TriggerTask> UpdateAsync(TriggerTask task, CancellationToken cancellationToken = default);
    public Task<TriggerTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default);
    public Task<TriggerTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default);
}
