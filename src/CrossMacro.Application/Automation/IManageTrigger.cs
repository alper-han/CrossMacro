namespace CrossMacro.Application.Automation;

public interface IManageTrigger : IManageAutomationTasks<TriggerTask>
{
    public Task<TriggerTask> AddAsync(TriggerTask task, CancellationToken cancellationToken = default);
    public Task<TriggerTask> AddAsync(TriggerTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default);
    public Task<TriggerTask> UpdateAsync(TriggerTask task, CancellationToken cancellationToken = default);
    public Task<TriggerTask> UpdateAsync(TriggerTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default);
}
