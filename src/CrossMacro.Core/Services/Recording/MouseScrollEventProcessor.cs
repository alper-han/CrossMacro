using CrossMacro.Core.Models;
using CrossMacro.Native.UInput;
using Serilog;

namespace CrossMacro.Core.Services.Recording;

/// <summary>
/// Processes mouse scroll events (EV_REL with REL_WHEEL code)
/// Single Responsibility: Only handles mouse scroll events
/// </summary>
public class MouseScrollEventProcessor : IEventProcessor
{
    private const int REL_WHEEL = 8;
    
    public bool CanProcess(ushort eventType)
    {
        return eventType == UInputNative.EV_REL;
    }
    
    public MacroEvent? ProcessEvent(ushort eventType, ushort eventCode, int eventValue, long timestampMs, int currentX, int currentY)
    {
        if (eventType != UInputNative.EV_REL)
            return null;
            
        if (eventCode != REL_WHEEL)
            return null;
            
        var macroEvent = new MacroEvent
        {
            Timestamp = timestampMs,
            Type = EventType.Click,
            Button = eventValue > 0 ? MouseButton.ScrollUp : MouseButton.ScrollDown
        };
        
        Log.Debug("[MouseScrollEventProcessor] Scroll {Direction}", 
            eventValue > 0 ? "Up" : "Down");
            
        return macroEvent;
    }
    
    public void Reset()
    {
        // No state to reset for scroll processor
    }
}
