using System;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services.TextExpansion;

/// <summary>
/// Responsible for processing raw input events and converting them into character events.
/// Handles modifier tracking, debouncing, and keyboard layout mapping.
/// </summary>
public interface IInputProcessor
{
    /// <summary>
    /// Fired when a valid character is typed.
    /// </summary>
    /// <summary>
    /// Gets a value indicating whether any modifier keys (Shift, Alt, Ctrl, AltGr) are currently pressed.
    /// </summary>
    bool AreModifiersPressed { get; }

    event Action<char> CharacterReceived;

    /// <summary>
    /// Fired when a special key (like Backspace, Enter) that affects the buffer is pressed.
    /// </summary>
    event Action<int> SpecialKeyReceived;

    /// <summary>
    /// Process a raw input event.
    /// </summary>
    void ProcessEvent(InputCaptureEventArgs e);

    /// <summary>
    /// Resets state (modifiers, etc.)
    /// </summary>
    void Reset();
}
