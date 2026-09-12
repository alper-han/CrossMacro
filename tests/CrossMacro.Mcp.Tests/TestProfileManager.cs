namespace CrossMacro.Mcp.Tests;

internal sealed class TestProfileManager(ProfileInfo activeProfile) : IProfileManager
    {
        public ProfileInfo ActiveProfile { get; } = activeProfile;

        public IReadOnlyList<ProfileInfo> Profiles { get; } = [activeProfile];

        public event EventHandler<ProfileChangedEventArgs>? ProfileChanged
        {
            add => ArgumentNullException.ThrowIfNull(value);
            remove => ArgumentNullException.ThrowIfNull(value);
}
        public Task InitializeAsync() => Task.CompletedTask;

        public Task SwitchProfileAsync(string profileId) => throw new NotSupportedException();

        public Task<ProfileInfo> CreateProfileAsync(string displayName) => throw new NotSupportedException();

        public Task RenameProfileAsync(string profileId, string newDisplayName) => throw new NotSupportedException();

        public Task DeleteProfileAsync(string profileId) => throw new NotSupportedException();

        public string GetProfileDirectory(string profileId) => throw new NotSupportedException();
    }
