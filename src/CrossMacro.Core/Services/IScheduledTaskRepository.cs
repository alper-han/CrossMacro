
namespace CrossMacro.Core.Services;

/// <summary>
/// Repository for managing scheduled tasks persistence
/// </summary>
public interface IScheduledTaskRepository
{
    /// <summary>
    /// Loads all scheduled tasks from storage
    /// </summary>
    public Task<IReadOnlyList<ScheduledTask>> LoadAsync();

    /// <summary>
    /// Reloads all scheduled tasks from the supplied profile configuration directory.
    /// </summary>
    public Task ReloadAsync(string profileConfigDirectory) => LoadAsync();

    /// <summary>
    /// Saves all scheduled tasks to storage
    /// </summary>
    public Task SaveAsync(IEnumerable<ScheduledTask> tasks);
}
