namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>Thrown when playback is requested on a player that is already playing.</summary>
public sealed class PlaybackAlreadyInProgressException : InvalidOperationException
{
    private const string DefaultMessage = "Playback is already in progress";

    public PlaybackAlreadyInProgressException()
        : base(DefaultMessage)
    {
    }

    public PlaybackAlreadyInProgressException(string message)
        : base(message)
    {
    }

    public PlaybackAlreadyInProgressException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
