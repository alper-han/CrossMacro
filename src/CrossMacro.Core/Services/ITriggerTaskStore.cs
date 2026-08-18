
namespace CrossMacro.Core.Services;

/// <summary>
/// Snapshot and persistence port for window trigger tasks.
/// </summary>
public interface ITriggerTaskStore
{
    /// <summary>Gets a stable, read-only task snapshot.</summary>
    public IReadOnlyList<TriggerTask> Tasks { get; }

    public Task LoadAsync();

    public Task SaveAsync();
}
