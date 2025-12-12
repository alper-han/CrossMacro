using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Core.Services.Recording;

public class KeyboardEventProcessor : IEventProcessor
{
    public bool CanProcess(ushort eventType)
    {
        return eventType == InputEventCode.EV_KEY;
    }
    
    public MacroEvent? ProcessEvent(ushort eventType, ushort eventCode, int eventValue, long timestampMs, int currentX, int currentY)
    {
        if (eventType != InputEventCode.EV_KEY)
            return null;
            
        if (eventCode < 1 || eventCode > 255)
            return null;
            
        if (eventCode == InputEventCode.BTN_LEFT || 
            eventCode == InputEventCode.BTN_RIGHT || 
            eventCode == InputEventCode.BTN_MIDDLE)
            return null;
        
        if (eventValue != 0 && eventValue != 1)
            return null;
            
        var macroEvent = new MacroEvent
        {
            Timestamp = timestampMs,
            Type = eventValue == 1 ? EventType.KeyPress : EventType.KeyRelease,
            KeyCode = eventCode,
            Button = MouseButton.None
        };
        
        Log.Information("[KeyboardEventProcessor] Key event: {Type} Key={Code}", 
            macroEvent.Type, macroEvent.KeyCode);
            
        return macroEvent;
    }
    
    public void Reset()
    {
    }
}
