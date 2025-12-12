using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Core.Services.Recording;

public class MouseButtonEventProcessor : IEventProcessor
{
    public bool CanProcess(ushort eventType)
    {
        return eventType == InputEventCode.EV_KEY;
    }
    
    public MacroEvent? ProcessEvent(ushort eventType, ushort eventCode, int eventValue, long timestampMs, int currentX, int currentY)
    {
        if (eventType != InputEventCode.EV_KEY)
            return null;
            
        if (eventCode != InputEventCode.BTN_LEFT && 
            eventCode != InputEventCode.BTN_RIGHT && 
            eventCode != InputEventCode.BTN_MIDDLE)
            return null;
            
        var macroEvent = new MacroEvent
        {
            Timestamp = timestampMs,
            X = currentX,
            Y = currentY
        };
        
        if (eventCode == InputEventCode.BTN_LEFT) macroEvent.Button = MouseButton.Left;
        else if (eventCode == InputEventCode.BTN_RIGHT) macroEvent.Button = MouseButton.Right;
        else if (eventCode == InputEventCode.BTN_MIDDLE) macroEvent.Button = MouseButton.Middle;
        
        macroEvent.Type = eventValue == 1 ? EventType.ButtonPress : EventType.ButtonRelease;
        
        Log.Debug("[MouseButtonEventProcessor] Button {Button} {Type} at ({X}, {Y})", 
            macroEvent.Button, macroEvent.Type, macroEvent.X, macroEvent.Y);
            
        return macroEvent;
    }
    
    public void Reset()
    {
    }
}
