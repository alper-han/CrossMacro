namespace CrossMacro.Application.Profiles;

/// <summary>Resolves a profile by identifier, matching either its Id or its Name case-insensitively.</summary>
internal static class ProfileLookup
{
    public static ProfileInfo? Find(IEnumerable<ProfileInfo> profiles, string? identifier)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        return profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, identifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Name, identifier, StringComparison.OrdinalIgnoreCase));
    }
}
