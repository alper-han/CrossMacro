using CrossMacro.Core.Models;
using CrossMacro.Native.UInput;
using Serilog;

namespace CrossMacro.Core.Services.Recording;

/// <summary>
/// Processes mouse button events (EV_KEY for mouse buttons)
/// Single Responsibility: Only handles mouse button press/release events
/// </summary>
public class MouseButtonEventProcessor : IEventProcessor
{
    public bool CanProcess(ushort eventType)
    {
        return eventType == UInputNative.EV_KEY;
    }
    
    public MacroEvent? ProcessEvent(ushort eventType, ushort eventCode, int eventValue, long timestampMs, int currentX, int currentY)
    {
        if (eventType != UInputNative.EV_KEY)
            return null;
            
        // Only process mouse buttons
        if (eventCode != UInputNative.BTN_LEFT && 
            eventCode != UInputNative.BTN_RIGHT && 
            eventCode != UInputNative.BTN_MIDDLE)
            return null;
            
        var macroEvent = new MacroEvent
        {
            Timestamp = timestampMs,
            X = currentX,
            Y = currentY
        };
        
        if (eventCode == UInputNative.BTN_LEFT) macroEvent.Button = MouseButton.Left;
        else if (eventCode == UInputNative.BTN_RIGHT) macroEvent.Button = MouseButton.Right;
        else if (eventCode == UInputNative.BTN_MIDDLE) macroEvent.Button = MouseButton.Middle;
        
        macroEvent.Type = eventValue == 1 ? EventType.ButtonPress : EventType.ButtonRelease;
        
        Log.Debug("[MouseButtonEventProcessor] Button {Button} {Type} at ({X}, {Y})", 
            macroEvent.Button, macroEvent.Type, macroEvent.X, macroEvent.Y);
            
        return macroEvent;
    }
    
    public void Reset()
    {
        // No state to reset for button processor
    }
}
