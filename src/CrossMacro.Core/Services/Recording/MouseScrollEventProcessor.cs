using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Core.Services.Recording;

public class MouseScrollEventProcessor : IEventProcessor
{
    public bool CanProcess(ushort eventType)
    {
        return eventType == InputEventCode.EV_REL;
    }
    
    public MacroEvent? ProcessEvent(ushort eventType, ushort eventCode, int eventValue, long timestampMs, int currentX, int currentY)
    {
        if (eventType != InputEventCode.EV_REL)
            return null;
            
        if (eventCode != InputEventCode.REL_WHEEL)
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
    }
}
