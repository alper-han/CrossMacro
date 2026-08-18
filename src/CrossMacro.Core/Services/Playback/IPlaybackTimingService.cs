
namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Handles timing delays during playback.
/// Supports pause-aware waiting with high-precision spin-wait for small delays.
/// </summary>
public interface IPlaybackTimingService
{
    /// <summary>
    /// Wait for specified delay with pause awareness
    /// </summary>
    /// <param name="delayMilliseconds">Delay in milliseconds</param>
    /// <param name="pauseToken">Token to check for pause state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task WaitAsync(double delayMilliseconds, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken);
}
