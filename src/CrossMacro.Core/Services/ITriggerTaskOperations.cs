
namespace CrossMacro.Core.Services;

/// <summary>
/// Application-facing operations for window trigger tasks.
/// </summary>
/// <remarks>
/// Task snapshots and persistence are supplied by <see cref="ITriggerTaskStore"/>.
/// Monitoring lifecycle and trigger events remain behind
/// <see cref="ITriggerService"/>; this port is limited to task mutations.
/// </remarks>
public interface ITriggerTaskOperations
{
    public void AddTask(TriggerTask task);

    public void RemoveTask(Guid id);

    public void UpdateTask(TriggerTask task);

    public void SetTaskEnabled(Guid id, bool enabled);
}
