namespace CrossMacro.Core.Services.Automation;

/// <summary>
/// Snapshot and persistence port of one automation task kind (schedules, shortcuts or triggers).
/// </summary>
public interface IAutomationTaskStore<TTask>
    where TTask : class
{
    /// <summary>Gets a stable, read-only task snapshot.</summary>
    public IReadOnlyList<TTask> Tasks { get; }

    /// <summary>Ensures the active profile has been loaded without replacing initialized runtime tasks.</summary>
    public Task LoadAsync();

    public Task SaveAsync();

    /// <summary>Persists a detached snapshot before publishing it to the runtime collection.
    /// Cancellation is observed before persistence commits; a completed write is always published.</summary>
    public Task CommitAsync(IReadOnlyList<TTask> tasks, CancellationToken cancellationToken = default);
}
