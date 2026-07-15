
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
    /// Reloads profile-specific settings from a profile configuration directory.
    /// </summary>
    public Task ReloadAsync(string profileConfigDirectory) => Task.CompletedTask;

    /// <summary>
    /// Saves current settings to disk synchronously
    /// </summary>
    public void Save();
}
