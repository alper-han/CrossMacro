using System;

namespace CrossMacro.Core.Models;

/// <summary>
/// Metadata for a single user profile.
/// </summary>
public class ProfileInfo
{
    /// <summary>
    /// Stable filesystem-safe identifier. Never changes after creation.
    /// </summary>
    public string Id { get; set; } = "default";

    /// <summary>
    /// User-visible display name. Can be renamed.
    /// </summary>
    public string Name { get; set; } = "Default";

    /// <summary>
    /// When the profile was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
