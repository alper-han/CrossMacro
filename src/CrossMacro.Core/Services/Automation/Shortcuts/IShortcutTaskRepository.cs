namespace CrossMacro.Core.Services.Automation.Shortcuts;

/// <summary>Profile-scoped persistence for shortcut task snapshots.</summary>
public interface IShortcutTaskRepository
{
    public Task<IReadOnlyList<ShortcutTask>?> LoadAsync(CancellationToken cancellationToken = default);
    public Task SaveAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken = default);
    public void SetProfileDirectory(string profileConfigDirectory);
}
