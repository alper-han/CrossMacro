using System;

namespace CrossMacro.Core.Models;

/// <summary>
/// Represents a single mouse event in a macro sequence
/// </summary>
public class MacroEvent
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
}

/// <summary>
/// Types of mouse events
/// </summary>
public enum EventType
{
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
    Click
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
    ScrollDown = 5
}
