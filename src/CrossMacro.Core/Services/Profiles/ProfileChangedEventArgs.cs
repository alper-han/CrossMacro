namespace CrossMacro.Core.Services.Profiles;

public sealed class ProfileChangedEventArgs(ProfileInfo profile) : EventArgs
{
    public ProfileInfo Profile { get; } = profile ?? throw new ArgumentNullException(nameof(profile));
}
