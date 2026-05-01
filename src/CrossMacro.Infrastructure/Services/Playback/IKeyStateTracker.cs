using System;
using System.Collections.Generic;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Tracks pressed keyboard key state for playback.
/// Enables pause/resume with state preservation.
/// </summary>
public interface IKeyStateTracker
{
    /// <summary>
    /// Record a key press
    /// </summary>
    void Press(int keyCode);

    /// <summary>
    /// Record a key release
    /// </summary>
    void Release(int keyCode);

    /// <summary>
    /// Get all currently pressed keys
    /// </summary>
    IReadOnlyCollection<int> PressedKeys { get; }

    /// <summary>
    /// Release all tracked keys via simulator and clear state
    /// </summary>
    void ReleaseAll(IInputSimulator simulator);

    /// <summary>
    /// Restore all previously pressed keys via simulator
    /// </summary>
    void RestoreAll(IInputSimulator simulator, IEnumerable<int> keys);

    /// <summary>
    /// Clear all tracking state without sending any events
    /// </summary>
    void Clear();
}
