
namespace CrossMacro.Core.Services.Automation.Triggers;

/// <summary>
/// Snapshot and persistence port for window trigger tasks.
/// </summary>
public interface ITriggerTaskStore
{
    /// <summary>Gets a stable, read-only task snapshot.</summary>
    public IReadOnlyList<TriggerTask> Tasks { get; }

    /// <summary>Ensures the active profile has been loaded without replacing initialized runtime tasks.</summary>
    public Task LoadAsync();

    public Task SaveAsync();

    /// <summary>Persists a detached snapshot before publishing it to the runtime collection.
    /// Cancellation is observed before persistence commits; a completed write is always published.</summary>
    public Task CommitAsync(IReadOnlyList<TriggerTask> tasks, CancellationToken cancellationToken = default);
}
