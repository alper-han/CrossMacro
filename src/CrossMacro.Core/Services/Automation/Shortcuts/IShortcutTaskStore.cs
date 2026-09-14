
namespace CrossMacro.Core.Services.Automation.Shortcuts;

/// <summary>
/// Snapshot and persistence port for shortcut tasks.
/// </summary>
public interface IShortcutTaskStore
{
    /// <summary>Gets a stable, read-only task snapshot.</summary>
    public IReadOnlyList<ShortcutTask> Tasks { get; }

    /// <summary>Ensures the active profile has been loaded without replacing initialized runtime tasks.</summary>
    public Task LoadAsync();

    public Task SaveAsync();

    /// <summary>Persists a detached snapshot before publishing it to the runtime collection.
    /// Cancellation is observed before persistence commits; a completed write is always published.</summary>
    public Task CommitAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken = default);
}
