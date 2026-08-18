
namespace CrossMacro.Core.Services;

/// <summary>
/// Snapshot and persistence port for scheduled tasks.
/// </summary>
public interface IScheduledTaskStore
{
    /// <summary>Gets a stable, read-only task snapshot.</summary>
    public IReadOnlyList<ScheduledTask> Tasks { get; }

    public Task LoadAsync();

    public Task SaveAsync();
}
