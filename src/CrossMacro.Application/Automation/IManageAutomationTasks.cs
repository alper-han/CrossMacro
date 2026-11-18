namespace CrossMacro.Application.Automation;

/// <summary>The operations every automation task kind supports.</summary>
public interface IManageAutomationTasks<TTask>
    where TTask : class
{
    public Task<TaskCollectionResult<TTask>> ListAsync(CancellationToken cancellationToken = default);
    public Task<TTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default);
    public Task<TTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default);
}
