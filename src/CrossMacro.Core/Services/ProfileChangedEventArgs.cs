namespace CrossMacro.Core.Services;

public sealed class ProfileChangedEventArgs(ProfileInfo profile) : EventArgs
{
    public ProfileInfo Profile { get; } = profile ?? throw new ArgumentNullException(nameof(profile));
}
