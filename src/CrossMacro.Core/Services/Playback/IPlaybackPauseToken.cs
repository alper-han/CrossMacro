
namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Token for pause state checking during waits.
/// </summary>
public interface IPlaybackPauseToken
{
    /// <summary>
    /// Whether playback is currently paused
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// Wait for resume if paused
    /// </summary>
    Task WaitIfPausedAsync(CancellationToken cancellationToken);
}
