namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>Runtime-owned high-precision playback timing implementation.</summary>
internal sealed class SystemPlaybackTimingService : IPlaybackTimingService
{
    public Task WaitAsync(
        double delayMilliseconds,
        IPlaybackPauseToken pauseToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pauseToken);
        return HighResolutionDelay.WaitAsync(delayMilliseconds, pauseToken, cancellationToken);
    }
}
