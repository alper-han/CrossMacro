using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Manages user profiles: registry, CRUD, migration, and live switching.
/// </summary>
public interface IProfileManager
{
    /// <summary>
    /// The currently active profile info.
    /// </summary>
    ProfileInfo ActiveProfile { get; }

    /// <summary>
    /// All available profiles.
    /// </summary>
    IReadOnlyList<ProfileInfo> Profiles { get; }

    /// <summary>
    /// Raised after a profile switch completes and all services have reloaded.
    /// Carries the new active profile info.
    /// </summary>
    event EventHandler<ProfileInfo>? ProfileChanged;

    /// <summary>
    /// Initializes the profile system: creates registry if missing,
    /// runs first-run migration from flat config files, ensures default profile exists.
    /// Must be called once during app startup before other services load.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Switches the active profile. Stops runtime services, reloads all
    /// profile-backed storage from the target profile directory, and restarts
    /// eligible services.
    /// </summary>
    Task SwitchProfileAsync(string profileId);

    /// <summary>
    /// Creates a new profile with the given display name.
    /// Returns the created profile info.
    /// </summary>
    Task<ProfileInfo> CreateProfileAsync(string displayName);

    /// <summary>
    /// Renames a profile's display name. The folder/id stays stable.
    /// </summary>
    Task RenameProfileAsync(string profileId, string newDisplayName);

    /// <summary>
    /// Deletes a profile. Cannot delete the active profile or the "default" profile.
    /// </summary>
    Task DeleteProfileAsync(string profileId);

    /// <summary>
    /// Gets the config directory path for a specific profile.
    /// </summary>
    string GetProfileDirectory(string profileId);
}
