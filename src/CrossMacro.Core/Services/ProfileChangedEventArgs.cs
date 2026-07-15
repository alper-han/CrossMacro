namespace CrossMacro.Core.Services;

public sealed class ProfileChangedEventArgs : EventArgs
{
    public ProfileChangedEventArgs(ProfileInfo profile)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    public ProfileInfo Profile { get; }
}
