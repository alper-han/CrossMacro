using CrossMacro.Core.Models;
using CrossMacro.Native.UInput;
using Serilog;

namespace CrossMacro.Core.Services.Recording;

/// <summary>
/// Processes keyboard events (EV_KEY for non-mouse keys)
/// Single Responsibility: Only handles keyboard key press/release events
/// </summary>
public class KeyboardEventProcessor : IEventProcessor
{
    public bool CanProcess(ushort eventType)
    {
        return eventType == UInputNative.EV_KEY;
    }
    
    public MacroEvent? ProcessEvent(ushort eventType, ushort eventCode, int eventValue, long timestampMs, int currentX, int currentY)
    {
        if (eventType != UInputNative.EV_KEY)
            return null;
            
        // Only process keyboard keys (KEY_ range is 1-255), not mouse buttons
        if (eventCode < 1 || eventCode > 255)
            return null;
            
        // Mouse buttons are handled by MouseEventProcessor
        if (eventCode == UInputNative.BTN_LEFT || 
            eventCode == UInputNative.BTN_RIGHT || 
            eventCode == UInputNative.BTN_MIDDLE)
            return null;
        
        // Only record press (value=1) and release (value=0), ignore repeat (value=2)
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
        // No state to reset for keyboard processor
    }
}
