
namespace CrossMacro.Core.Services;

/// <summary>
/// Service for managing application settings with persistence
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current application settings
    /// </summary>
    public AppSettings Current { get; }

    /// <summary>
    /// Loads settings from disk asynchronously
    /// </summary>
    public Task<AppSettings> LoadAsync();

    /// <summary>
    /// Loads settings from disk synchronously
    /// </summary>
    public AppSettings Load();

    /// <summary>
    /// Saves current settings to disk
    /// </summary>
    public Task SaveAsync();

    /// <summary>
    /// Schedules a settings save after the current burst of changes has settled.
    /// Implementations may coalesce consecutive requests.
    /// </summary>
    public Task SaveAfterIdleAsync() => SaveAsync();

    /// <summary>
    /// Flushes a pending idle save before a profile switch or shutdown.
    /// </summary>
    public Task FlushPendingSaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Reloads profile-specific settings from a profile configuration directory.
    /// </summary>
    public Task ReloadAsync(string profileConfigDirectory) => Task.CompletedTask;

    /// <summary>
    /// Saves current settings to disk synchronously
    /// </summary>
    public void Save();
}
