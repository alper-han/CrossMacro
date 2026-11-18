
namespace CrossMacro.Core.Services.Automation.Scheduling;

/// <summary>
/// Application-facing operations for scheduled tasks.
/// </summary>
/// <remarks>
/// This port contains manual execution only. Task changes, the snapshot and the
/// persistence lifecycle are supplied by <see cref="IAutomationTaskStore{TTask}"/>.
/// </remarks>
public interface IScheduledTaskOperations
{
    /// <summary>Synchronously resolves and binds the selected task before returning its execution task.</summary>
    /// <remarks>Callers may release the active-profile admission gate once this method returns; execution completion may be asynchronous.</remarks>
    public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
}
