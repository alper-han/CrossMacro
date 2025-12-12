using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Interface for macro playback service
/// </summary>
public interface IMacroPlayer : IDisposable
{
    /// <summary>
    /// Whether playback is currently active
    /// </summary>
    /// <summary>
    /// Whether playback is currently paused
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// Current loop iteration (1-based)
    /// </summary>
    int CurrentLoop { get; }

    /// <summary>
    /// Total number of loops (0 = infinite)
    /// </summary>
    int TotalLoops { get; }

    /// <summary>
    /// Whether the player is currently waiting between loop iterations
    /// </summary>
    bool IsWaitingBetweenLoops { get; }

    /// <summary>
    /// Plays a macro sequence
    /// </summary>
    Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stops the currently playing macro
    /// </summary>
    void Stop();

    /// <summary>
    /// Pauses the currently playing macro
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes the paused macro
    /// </summary>
    void Resume();
}
