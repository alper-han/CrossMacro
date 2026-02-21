using System;

namespace CrossMacro.Core.Models;

/// <summary>
/// Represents a single mouse event in a macro sequence
/// </summary>
public struct MacroEvent
{
    /// <summary>
    /// Type of the event
    /// </summary>
    public EventType Type { get; set; }
    
    /// <summary>
    /// X coordinate (for move events)
    /// </summary>
    public int X { get; set; }
    
    /// <summary>
    /// Y coordinate (for move events)
    /// </summary>
    public int Y { get; set; }
    
    /// <summary>
    /// Mouse button (for click events)
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
    /// Keyboard key code (for key press/release events)
    /// Uses Linux input key codes (e.g., 30 = KEY_A, 57 = KEY_SPACE)
    /// </summary>
    public int KeyCode { get; set; }
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
    /// Mouse moved to absolute position
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
