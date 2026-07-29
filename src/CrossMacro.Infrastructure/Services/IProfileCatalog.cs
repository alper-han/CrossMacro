namespace CrossMacro.Infrastructure.Services;

internal interface IProfileCatalog : IDisposable
{
    public ProfileInfo ActiveProfile { get; }
    public IReadOnlyList<ProfileInfo> Profiles { get; }
    public Task InitializeAsync();
    public Task SetActiveProfileAsync(string profileId);
    public void RestoreActiveProfile(string profileId);
    public Task<ProfileInfo> CreateProfileAsync(string displayName);
    public Task RenameProfileAsync(string profileId, string newDisplayName);
    public Task DeleteProfileAsync(string profileId);
    public string GetProfileDirectory(string profileId);
}
