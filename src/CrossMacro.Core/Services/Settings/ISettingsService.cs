
namespace CrossMacro.Core.Services.Settings;

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
    /// Reads or mutates current settings synchronously. Concurrent implementations must serialize
    /// this callback with persistence snapshots and profile publication. Do not await in the callback
    /// or retain the mutable settings reference beyond it.
    /// </summary>
    public T AccessCurrent<T>(Func<AppSettings, T> access);

    /// <summary>
    /// Loads settings from disk asynchronously
    /// </summary>
    public Task<AppSettings> LoadAsync();

    /// <summary>
    /// Initializes settings once without replacing pending in-memory changes.
    /// </summary>
    public Task<AppSettings> EnsureLoadedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves current settings to disk
    /// </summary>
    public Task SaveAsync();

    /// <summary>
    /// Schedules a settings save after the current burst of changes has settled.
    /// Implementations may coalesce consecutive requests.
    /// </summary>
    public Task SaveAfterIdleAsync();

    /// <summary>
    /// Flushes a pending idle save before a profile switch or shutdown.
    /// </summary>
    public Task FlushPendingSaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads profile-specific settings from a profile configuration directory.
    /// </summary>
    public Task ReloadAsync(string profileConfigDirectory);
}
