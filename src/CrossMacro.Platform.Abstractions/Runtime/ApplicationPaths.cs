namespace CrossMacro.Platform.Abstractions.Runtime;

/// <summary>An immutable application path snapshot supplied by the host.</summary>
public sealed record ApplicationPaths(string ConfigDirectory)
{
    public string GetConfigFilePath(string fileName) => Path.Combine(ConfigDirectory, fileName);
    public string GetProfilesDirectory() => Path.Combine(ConfigDirectory, "profiles");
    public string GetProfileDirectory(string profileId) => Path.Combine(GetProfilesDirectory(), profileId);
}
