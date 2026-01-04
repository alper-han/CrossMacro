using System;
using System.Collections.Generic;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Converts between EditorAction and MacroEvent/MacroSequence.
/// Handles bidirectional conversion while preserving .macro format compatibility.
/// </summary>
public class EditorActionConverter : IEditorActionConverter
{
    private const int DefaultKeyPressDelayMs = 10;
    
    /// <inheritdoc/>
    public List<MacroEvent> ToMacroEvents(EditorAction action)
    {
        var events = new List<MacroEvent>();
        
        switch (action.Type)
        {
            case EditorActionType.MouseMove:
                events.Add(new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = action.X,
                    Y = action.Y,
                    DelayMs = action.DelayMs
                });
                break;
                
            case EditorActionType.MouseClick:
                events.Add(new MacroEvent
                {
                    Type = EventType.Click,
                    X = action.X,
                    Y = action.Y,
                    Button = action.Button,
                    DelayMs = action.DelayMs
                });
                break;
                
            case EditorActionType.MouseDown:
                events.Add(new MacroEvent
                {
                    Type = EventType.ButtonPress,
                    X = action.X,
                    Y = action.Y,
                    Button = action.Button,
                    DelayMs = action.DelayMs
                });
                break;
                
            case EditorActionType.MouseUp:
                events.Add(new MacroEvent
                {
                    Type = EventType.ButtonRelease,
                    X = action.X,
                    Y = action.Y,
                    Button = action.Button,
                    DelayMs = action.DelayMs
                });
                break;
                
            case EditorActionType.KeyPress:
                // KeyPress expands to KeyDown + KeyUp
                events.Add(new MacroEvent
                {
                    Type = EventType.KeyPress,
                    KeyCode = action.KeyCode,
                    DelayMs = action.DelayMs
                });
                events.Add(new MacroEvent
                {
                    Type = EventType.KeyRelease,
                    KeyCode = action.KeyCode,
                    DelayMs = DefaultKeyPressDelayMs
                });
                break;
                
            case EditorActionType.KeyDown:
                events.Add(new MacroEvent
                {
                    Type = EventType.KeyPress,
                    KeyCode = action.KeyCode,
                    DelayMs = action.DelayMs
                });
                break;
                
            case EditorActionType.KeyUp:
                events.Add(new MacroEvent
                {
                    Type = EventType.KeyRelease,
                    KeyCode = action.KeyCode,
                    DelayMs = action.DelayMs
                });
                break;
                
            case EditorActionType.Delay:
                // Delay is added to the next event's DelayMs
                // Create a placeholder move event with the delay
                events.Add(new MacroEvent
                {
                    Type = EventType.None,
                    DelayMs = action.DelayMs
                });
                break;
                
            case EditorActionType.ScrollVertical:
                var scrollButton = action.ScrollAmount > 0 ? MouseButton.ScrollUp : MouseButton.ScrollDown;
                for (int i = 0; i < Math.Abs(action.ScrollAmount); i++)
                {
                    events.Add(new MacroEvent
                    {
                        Type = EventType.Click,
                        Button = scrollButton,
                        DelayMs = i == 0 ? action.DelayMs : 0
                    });
                }
                break;
                
            case EditorActionType.ScrollHorizontal:
                var hScrollButton = action.ScrollAmount > 0 ? MouseButton.ScrollRight : MouseButton.ScrollLeft;
                for (int i = 0; i < Math.Abs(action.ScrollAmount); i++)
                {
                    events.Add(new MacroEvent
                    {
                        Type = EventType.Click,
                        Button = hScrollButton,
                        DelayMs = i == 0 ? action.DelayMs : 0
                    });
                }
                break;
        }
        
        return events;
    }
    
    /// <inheritdoc/>
    public EditorAction FromMacroEvent(MacroEvent ev, MacroEvent? nextEvent = null)
    {
        var action = new EditorAction
        {
            DelayMs = ev.DelayMs
        };
        
        switch (ev.Type)
        {
            case EventType.MouseMove:
                action.Type = EditorActionType.MouseMove;
                action.X = ev.X;
                action.Y = ev.Y;
                // IsAbsolute will be set based on sequence metadata
                break;
                
            case EventType.Click:
                if (IsScrollButton(ev.Button))
                {
                    action.Type = ev.Button is MouseButton.ScrollUp or MouseButton.ScrollDown 
                        ? EditorActionType.ScrollVertical 
                        : EditorActionType.ScrollHorizontal;
                    action.ScrollAmount = ev.Button is MouseButton.ScrollUp or MouseButton.ScrollRight ? 1 : -1;
                }
                else
                {
                    action.Type = EditorActionType.MouseClick;
                    action.X = ev.X;
                    action.Y = ev.Y;
                    action.Button = ev.Button;
                }
                break;
                
            case EventType.ButtonPress:
                action.Type = EditorActionType.MouseDown;
                action.X = ev.X;
                action.Y = ev.Y;
                action.Button = ev.Button;
                break;
                
            case EventType.ButtonRelease:
                action.Type = EditorActionType.MouseUp;
                action.X = ev.X;
                action.Y = ev.Y;
                action.Button = ev.Button;
                break;
                
            case EventType.KeyPress:
                // Check if next event is KeyRelease with same key - then merge to KeyPress
                if (nextEvent?.Type == EventType.KeyRelease && nextEvent?.KeyCode == ev.KeyCode)
                {
                    action.Type = EditorActionType.KeyPress;
                }
                else
                {
                    action.Type = EditorActionType.KeyDown;
                }
                action.KeyCode = ev.KeyCode;
                break;
                
            case EventType.KeyRelease:
                action.Type = EditorActionType.KeyUp;
                action.KeyCode = ev.KeyCode;
                break;
                
            default:
                action.Type = EditorActionType.Delay;
                break;
        }
        
        return action;
    }
    
    /// <inheritdoc/>
    public MacroSequence ToMacroSequence(IEnumerable<EditorAction> actions, string name, bool isAbsolute, bool skipInitialZeroZero = false)
    {
        var sequence = new MacroSequence
        {
            Name = name,
            IsAbsoluteCoordinates = isAbsolute,
            SkipInitialZeroZero = skipInitialZeroZero,
            CreatedAt = DateTime.UtcNow
        };
        
        long timestamp = 0;
        int pendingDelay = 0;
        
        foreach (var action in actions)
        {
            var events = ToMacroEvents(action);
            
            foreach (var ev in events)
            {
                // Skip None type events but accumulate their delay
                if (ev.Type == EventType.None)
                {
                    pendingDelay += ev.DelayMs;
                    continue;
                }
                
                var eventToAdd = ev;
                eventToAdd.DelayMs += pendingDelay;
                eventToAdd.Timestamp = timestamp;
                
                timestamp += eventToAdd.DelayMs;
                pendingDelay = 0;
                
                sequence.Events.Add(eventToAdd);
            }
        }
        
        sequence.CalculateDuration();
        sequence.MouseMoveCount = sequence.Events.Count(e => e.Type == EventType.MouseMove);
        sequence.ClickCount = sequence.Events.Count(e => e.Type != EventType.MouseMove);
        
        return sequence;
    }
    
    /// <inheritdoc/>
    public List<EditorAction> FromMacroSequence(MacroSequence sequence)
    {
        var actions = new List<EditorAction>();
        var events = sequence.Events;
        
        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            var nextEvent = i + 1 < events.Count ? events[i + 1] : (MacroEvent?)null;
            
            // Skip KeyRelease if it was merged with previous KeyPress
            if (ev.Type == EventType.KeyRelease && i > 0)
            {
                var prevAction = actions.LastOrDefault();
                if (prevAction?.Type == EditorActionType.KeyPress && prevAction.KeyCode == ev.KeyCode)
                {
                    continue; // Already merged
                }
            }
            
            var action = FromMacroEvent(ev, nextEvent);
            
            // Set IsAbsolute based on sequence metadata for MouseMove actions
            if (action.Type == EditorActionType.MouseMove)
            {
                action.IsAbsolute = sequence.IsAbsoluteCoordinates;
            }
            
            actions.Add(action);
        }
        
        return actions;
    }
    
    private static bool IsScrollButton(MouseButton button)
    {
        return button is MouseButton.ScrollUp or MouseButton.ScrollDown 
            or MouseButton.ScrollLeft or MouseButton.ScrollRight;
    }
}
