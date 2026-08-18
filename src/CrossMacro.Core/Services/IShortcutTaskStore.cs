
namespace CrossMacro.Core.Services;

/// <summary>
/// Snapshot and persistence port for shortcut tasks.
/// </summary>
public interface IShortcutTaskStore
{
    /// <summary>Gets a stable, read-only task snapshot.</summary>
    public IReadOnlyList<ShortcutTask> Tasks { get; }

    public Task LoadAsync();

    public Task SaveAsync();
}
