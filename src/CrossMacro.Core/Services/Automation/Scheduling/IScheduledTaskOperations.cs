
namespace CrossMacro.Core.Services.Automation.Scheduling;

/// <summary>
/// Application-facing operations for scheduled tasks.
/// </summary>
/// <remarks>
/// This port contains task mutations and manual execution only. The task
/// snapshot and persistence lifecycle are supplied by
/// <see cref="IScheduledTaskStore"/>.
/// </remarks>
public interface IScheduledTaskOperations
{
    public void AddTask(ScheduledTask task);

    public void RemoveTask(Guid id);

    public void UpdateTask(ScheduledTask task);

    public void SetTaskEnabled(Guid id, bool enabled);

    /// <summary>Synchronously resolves and binds the selected task before returning its execution task.</summary>
    /// <remarks>Callers may release the active-profile admission gate once this method returns; execution completion may be asynchronous.</remarks>
    public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
}
