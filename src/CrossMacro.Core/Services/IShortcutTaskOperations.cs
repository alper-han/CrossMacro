
namespace CrossMacro.Core.Services;

/// <summary>
/// Application-facing operations for shortcut tasks.
/// </summary>
/// <remarks>
/// Task snapshots and persistence are supplied by <see cref="IShortcutTaskStore"/>.
/// Shortcut listening lifecycle, events and hotkey integration remain behind
/// <see cref="IShortcutService"/>.
/// </remarks>
public interface IShortcutTaskOperations
{
    public void AddTask(ShortcutTask task);

    public void RemoveTask(Guid id);

    public void UpdateTask(ShortcutTask task);

    public void SetTaskEnabled(Guid id, bool enabled);

    public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
}
