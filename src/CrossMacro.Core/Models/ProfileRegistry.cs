using System.Text.Json.Serialization;

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
    /// All known profiles. Mutation is centralized through
    /// <see cref="ReplaceProfiles"/> to preserve the invariant that the
    /// collection is never null and never replaced out from under callers.
    /// The getter-only property plus <see cref="ProfileRegistry(int,string,IList{ProfileInfo})"/>
    /// constructor lets the JSON source generator round-trip the object
    /// without a public setter.
    /// </summary>
    public IList<ProfileInfo> Profiles { get; } = [];

    public ProfileRegistry()
    {
    }

    [JsonConstructor]
    public ProfileRegistry(int version, string activeProfile, IList<ProfileInfo>? profiles)
    {
        Version = version;
        ActiveProfile = activeProfile;
        // ReplaceProfiles validates (non-null) and rebuilds the backing list
        // in place so the collection identity stays stable across deserialization.
        if (profiles is null)
        {
            // Defensive: a missing/null JSON token projects to an empty registry
            // rather than a null backing collection. Profiles is already non-null.
            return;
        }
        ReplaceProfiles(profiles);
    }

    public void ReplaceProfiles(IEnumerable<ProfileInfo> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        var replacement = profiles.ToArray();
        Profiles.Clear();

        foreach (var profile in replacement)
        {
            Profiles.Add(profile);
        }
    }
}
