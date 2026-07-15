
namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Token for pause state checking during waits.
/// </summary>
public interface IPlaybackPauseToken
{
    /// <summary>
    /// Whether playback is currently paused
    /// </summary>
    public bool IsPaused { get; }

    /// <summary>
    /// Wait for resume if paused
    /// </summary>
    public Task WaitIfPausedAsync(CancellationToken cancellationToken);
}
