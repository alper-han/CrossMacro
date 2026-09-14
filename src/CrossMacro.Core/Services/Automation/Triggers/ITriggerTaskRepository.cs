namespace CrossMacro.Core.Services.Automation.Triggers;

/// <summary>Profile-scoped persistence for trigger task snapshots.</summary>
public interface ITriggerTaskRepository
{
    public Task<IReadOnlyList<TriggerTask>?> LoadAsync(CancellationToken cancellationToken = default);
    public Task SaveAsync(IReadOnlyList<TriggerTask> tasks, CancellationToken cancellationToken = default);
    public void SetProfileDirectory(string profileConfigDirectory);
}
