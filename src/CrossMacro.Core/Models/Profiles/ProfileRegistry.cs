using System.Text.Json.Serialization;

namespace CrossMacro.Core.Models.Profiles;

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
    /// All known profiles. The collection is never null and keeps its identity; replace its
    /// contents through <see cref="ReplaceProfiles"/>. The JSON constructor populates it on load.
    /// </summary>
    public IList<ProfileInfo> Profiles { get; } = [];

    public ProfileRegistry() { /* Empty */ }

    [JsonConstructor]
    public ProfileRegistry(int version, string activeProfile, IList<ProfileInfo>? profiles)
    {
        Version = version;
        ActiveProfile = activeProfile;
        if (profiles is not null)
        {
            ReplaceProfiles(profiles);
        }
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
