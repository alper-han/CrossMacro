using CrossMacro.Core.Models.Playback;

namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Interface for macro playback service
/// </summary>
public interface IMacroPlayer : IDisposable
{
    /// <summary>
    /// Whether playback is currently active
    /// </summary>
    public bool IsPlaying { get; }

    /// <summary>
    /// Whether playback is currently paused
    /// </summary>
    public bool IsPaused { get; }

    /// <summary>
    /// Current loop iteration (1-based)
    /// </summary>
    public int CurrentLoop { get; }

    /// <summary>
    /// Total number of loops (0 = infinite)
    /// </summary>
    public int TotalLoops { get; }

    /// <summary>
    /// Whether the player is currently waiting between loop iterations
    /// </summary>
    public bool IsWaitingBetweenLoops { get; }

    /// <summary>
    /// Raised when playback starts, pauses, resumes or ends (completion, stop or failure), when the loop
    /// counter advances and when the player enters or leaves the wait between loops. Raised once per
    /// state change on the playback thread, never per input event; subscribers must return quickly
    /// and marshal to their own thread.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Plays a macro sequence
    /// </summary>
    public Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the currently playing macro
    /// </summary>
    public void StopPlayback();

    /// <summary>
    /// Pauses the currently playing macro
    /// </summary>
    public void Pause();

    /// <summary>
    /// Resumes the paused macro
    /// </summary>
    public void ResumePlayback();
}
