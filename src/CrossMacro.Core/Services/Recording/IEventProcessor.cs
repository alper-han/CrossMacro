using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services.Recording;

/// <summary>
/// Interface for processing native evdev events into MacroEvents
/// </summary>
public interface IEventProcessor
{
    /// <summary>
    /// Process a native event and optionally return a MacroEvent
    /// </summary>
    /// <param name="eventType">Event type (EV_KEY, EV_REL, etc.)</param>
    /// <param name="eventCode">Event code</param>
    /// <param name="eventValue">Event value</param>
    /// <param name="timestampMs">Current timestamp in milliseconds</param>
    /// <param name="currentX">Current cached X position</param>
    /// <param name="currentY">Current cached Y position</param>
    /// <returns>MacroEvent if event should be recorded, null otherwise</returns>
    MacroEvent? ProcessEvent(ushort eventType, ushort eventCode, int eventValue, long timestampMs, int currentX, int currentY);
    
    /// <summary>
    /// Whether this processor handles the given event type
    /// </summary>
    bool CanProcess(ushort eventType);
    
    /// <summary>
    /// Reset any internal state (called when recording starts)
    /// </summary>
    void Reset();
}
