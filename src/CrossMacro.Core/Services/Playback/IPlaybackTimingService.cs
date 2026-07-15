using System.Threading;
using System.Threading.Tasks;

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

/// <summary>
/// Handles timing delays during playback.
/// Supports pause-aware waiting with high-precision spin-wait for small delays.
/// </summary>
public interface IPlaybackTimingService
{
    /// <summary>
    /// Wait for specified delay with pause awareness
    /// </summary>
    /// <param name="delayMs">Delay in milliseconds</param>
    /// <param name="pauseToken">Token to check for pause state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WaitAsync(int delayMs, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken);
}
