using System;

namespace CrossMacro.Core.Models;

/// <summary>
/// Represents a single input event in a macro sequence.
/// </summary>
public struct MacroEvent
{
    /// <summary>
    /// Type of the event
    /// </summary>
    public EventType Type { get; set; }
    
    /// <summary>
    /// X coordinate or horizontal delta for coordinate-bearing mouse events.
    /// The value is interpreted by the effective coordinate mode resolved from
    /// <see cref="CoordinateMode" /> or the macro's legacy coordinate metadata.
    /// </summary>
    public int X { get; set; }
    
    /// <summary>
    /// Y coordinate or vertical delta for coordinate-bearing mouse events.
    /// The value is interpreted by the effective coordinate mode resolved from
    /// <see cref="CoordinateMode" /> or the macro's legacy coordinate metadata.
    /// </summary>
    public int Y { get; set; }
    
    /// <summary>
    /// Mouse button for button press, button release, click, and scroll events.
    /// </summary>
    public MouseButton Button { get; set; }
    
    /// <summary>
    /// Timestamp when the event was recorded (milliseconds since recording start)
    /// </summary>
    public long Timestamp { get; set; }
    
    /// <summary>
    /// Delay until next event (milliseconds)
    /// </summary>
    public int DelayMs { get; set; }

    /// <summary>
    /// Whether the delay includes a randomized component.
    /// </summary>
    public bool HasRandomDelay { get; set; }

    /// <summary>
    /// Minimum randomized delay in milliseconds.
    /// </summary>
    public int RandomDelayMinMs { get; set; }

    /// <summary>
    /// Maximum randomized delay in milliseconds.
    /// </summary>
    public int RandomDelayMaxMs { get; set; }
    
    /// <summary>
    /// Keyboard key code for key press and key release events.
    /// Uses Linux input key codes (e.g., 30 = KEY_A, 57 = KEY_SPACE)
    /// </summary>
    public int KeyCode { get; set; }

    /// <summary>
    /// Optional event-level coordinate mode for coordinate-bearing mouse events.
    /// When unset, the macro-wide legacy coordinate metadata is used as the fallback.
    /// Ignored for keyboard events, scroll events, and current-position mouse button events.
    /// </summary>
    public MouseCoordinateMode? CoordinateMode { get; set; }

    /// <summary>
    /// Whether a non-scroll mouse button event should use the live cursor
    /// position at playback time instead of the stored coordinates.
    /// </summary>
    public bool UseCurrentPosition { get; set; }
}

/// <summary>
/// Types of mouse events
/// </summary>
public enum EventType
{
    /// <summary>
    /// No event / default state
    /// </summary>
    None = 0,

    /// <summary>
    /// Mouse button pressed
    /// </summary>
    ButtonPress,
    
    /// <summary>
    /// Mouse button released
    /// </summary>
    ButtonRelease,
    
    /// <summary>
    /// Mouse moved by coordinates or deltas interpreted by the effective coordinate mode.
    /// </summary>
    MouseMove,
    
    /// <summary>
    /// Mouse click (press + release)
    /// </summary>
    Click,
    
    /// <summary>
    /// Keyboard key pressed
    /// </summary>
    KeyPress,
    
    /// <summary>
    /// Keyboard key released
    /// </summary>
    KeyRelease
}

/// <summary>
/// Mouse buttons
/// </summary>
public enum MouseButton
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 3,
    ScrollUp = 4,
    ScrollDown = 5,
    ScrollLeft = 6,
    ScrollRight = 7,
    Side1 = 8,
    Side2 = 9
}
