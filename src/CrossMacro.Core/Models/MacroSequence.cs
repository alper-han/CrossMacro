using System;
using System.Collections.Generic;
using System.Linq;

namespace CrossMacro.Core.Models;

/// <summary>
/// Represents a complete macro sequence
/// </summary>
public class MacroSequence
{
    /// <summary>
    /// Unique identifier for this macro
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Name of the macro
    /// </summary>
    public string Name { get; set; } = "Unnamed Macro";
    
    /// <summary>
    /// List of events in the macro
    /// </summary>
    public List<MacroEvent> Events { get; set; } = new();
    
    /// <summary>
    /// When the macro was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Total duration of the macro in milliseconds
    /// </summary>
    public long TotalDurationMs { get; set; }
    
    /// <summary>
    /// Number of events in the macro
    /// </summary>
    public int EventCount => Events.Count;
    
    // Statistics metadata
    /// <summary>
    /// When the macro was recorded
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Actual recording duration (wall clock time)
    /// </summary>
    public TimeSpan ActualDuration { get; set; }
    
    /// <summary>
    /// Number of mouse move events
    /// </summary>
    public int MouseMoveCount { get; set; }
    
    /// <summary>
    /// Number of click events
    /// </summary>
    public int ClickCount { get; set; }
    
    /// <summary>
    /// Events recorded per second
    /// </summary>
    public double EventsPerSecond { get; set; }

    /// <summary>
    /// Whether the macro contains absolute coordinates (true) or relative deltas (false)
    /// </summary>
    public bool IsAbsoluteCoordinates { get; set; }
    
    /// <summary>
    /// Validates the macro sequence
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        if (Events == null || Events.Count == 0)
            return false;
            
        // Check that all events have valid timestamps
        return Events.All(e => e.Timestamp >= 0 && e.DelayMs >= 0);
    }
    
    /// <summary>
    /// Calculates total duration from events
    /// </summary>
    public void CalculateDuration()
    {
        if (Events.Count == 0)
        {
            TotalDurationMs = 0;
            return;
        }
        
        TotalDurationMs = Events.Last().Timestamp;
    }
}
