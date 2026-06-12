using System.Collections.Generic;

namespace CrossMacro.Core.Models;

/// <summary>
/// Root object persisted in profile-registry.json.
/// Tracks available profiles and which one is active.
/// </summary>
public class ProfileRegistry
{
    /// <summary>
    /// Schema version for future migrations.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// The Id of the currently active profile.
    /// </summary>
    public string ActiveProfile { get; set; } = "default";

    /// <summary>
    /// All known profiles.
    /// </summary>
    public List<ProfileInfo> Profiles { get; set; } = [];
}
