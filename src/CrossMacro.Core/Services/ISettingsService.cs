using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Service for managing application settings with persistence
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current application settings
    /// </summary>
    AppSettings Current { get; }
    
    /// <summary>
    /// Loads settings from disk asynchronously
    /// </summary>
    Task<AppSettings> LoadAsync();

    /// <summary>
    /// Loads settings from disk synchronously
    /// </summary>
    AppSettings Load();
    
    /// <summary>
    /// Saves current settings to disk
    /// </summary>
    Task SaveAsync();
}
