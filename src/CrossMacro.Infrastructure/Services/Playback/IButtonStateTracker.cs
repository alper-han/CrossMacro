using System;
using System.Collections.Generic;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Tracks pressed mouse button state for playback.
/// Enables pause/resume with state preservation.
/// </summary>
public interface IButtonStateTracker
{
    /// <summary>
    /// Record a button press
    /// </summary>
    void Press(ushort button);

    /// <summary>
    /// Record a button release
    /// </summary>
    void Release(ushort button);

    /// <summary>
    /// Whether any button is currently pressed
    /// </summary>
    bool IsAnyPressed { get; }

    /// <summary>
    /// Get all currently pressed buttons
    /// </summary>
    IReadOnlyCollection<ushort> PressedButtons { get; }

    /// <summary>
    /// Release all tracked buttons via simulator and clear state
    /// </summary>
    void ReleaseAll(IInputSimulator simulator);

    /// <summary>
    /// Restore all previously pressed buttons via simulator
    /// </summary>
    void RestoreAll(IInputSimulator simulator, IEnumerable<ushort> buttons);

    /// <summary>
    /// Clear all tracking state without sending any events
    /// </summary>
    void Clear();
}
