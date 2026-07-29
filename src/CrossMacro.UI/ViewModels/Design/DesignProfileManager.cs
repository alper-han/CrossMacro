
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignProfileManager : IProfileManager
{
    public DesignProfileManager()
    {
        Profiles =
        [
            new ProfileInfo { Id = "default", Name = "Default" },
            new ProfileInfo { Id = "dev", Name = "Development" },
            new ProfileInfo { Id = "gaming", Name = "Gaming" },
        ];
        ActiveProfile = Profiles[0];
    }

    public ProfileInfo ActiveProfile { get; private set; }

    public IReadOnlyList<ProfileInfo> Profiles { get; }

    public event EventHandler<ProfileChangedEventArgs>? ProfileChanged
    {
        add { /* Empty */ }
        remove { /* Empty */ }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task SwitchProfileAsync(string profileId)
    {
        ActiveProfile = Profiles.FirstOrDefault(p => string.Equals(p.Id, profileId, StringComparison.Ordinal)) ?? Profiles[0];
        return Task.CompletedTask;
    }

    public Task<ProfileInfo> CreateProfileAsync(string displayName) =>
        Task.FromResult(new ProfileInfo { Id = NormalizeProfileId(displayName), Name = displayName });

    public Task RenameProfileAsync(string profileId, string newDisplayName) => Task.CompletedTask;

    public Task DeleteProfileAsync(string profileId) => Task.CompletedTask;

    public string GetProfileDirectory(string profileId) => $"/home/demo/.config/crossmacro/{profileId}";

    private static string NormalizeProfileId(string displayName)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToLower(displayName);
    }
}
