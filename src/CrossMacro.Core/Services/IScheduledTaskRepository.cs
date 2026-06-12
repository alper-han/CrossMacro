using System.Collections.Generic;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Repository for managing scheduled tasks persistence
/// </summary>
public interface IScheduledTaskRepository
{
    /// <summary>
    /// Loads all scheduled tasks from storage
    /// </summary>
    Task<List<ScheduledTask>> LoadAsync();

    /// <summary>
    /// Reloads all scheduled tasks from the supplied profile configuration directory.
    /// </summary>
    Task ReloadAsync(string profileConfigDirectory) => LoadAsync();

    /// <summary>
    /// Saves all scheduled tasks to storage
    /// </summary>
    Task SaveAsync(IEnumerable<ScheduledTask> tasks);
}
