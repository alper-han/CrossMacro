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
    
    private readonly IKeyCodeMapper _keyCodeMapper;
    
    public EditorActionConverter(IKeyCodeMapper keyCodeMapper)
    {
        _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
    }
    
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
                
            case EditorActionType.TextInput:
                bool isFirst = true;
                foreach (var c in action.Text)
                {
                    var keyCode = _keyCodeMapper.GetKeyCodeForCharacter(c);
                    if (keyCode == -1) continue; // Skip unmappable characters
                    
                    var needsShift = _keyCodeMapper.RequiresShift(c);
                    var needsAltGr = _keyCodeMapper.RequiresAltGr(c);
                    
                    // Press modifiers first
                    if (needsShift)
                    {
                        events.Add(new MacroEvent
                        {
                            Type = EventType.KeyPress,
                            KeyCode = InputEventCode.KEY_LEFTSHIFT,
                            DelayMs = 0
                        });
                    }
                    
                    if (needsAltGr)
                    {
                        events.Add(new MacroEvent
                        {
                            Type = EventType.KeyPress,
                            KeyCode = InputEventCode.KEY_RIGHTALT,
                            DelayMs = 0
                        });
                    }
                    
                    // Press and release the actual key
                    events.Add(new MacroEvent
                    {
                        Type = EventType.KeyPress,
                        KeyCode = keyCode,
                        DelayMs = isFirst ? action.DelayMs : DefaultKeyPressDelayMs
                    });
                    events.Add(new MacroEvent
                    {
                        Type = EventType.KeyRelease,
                        KeyCode = keyCode,
                        DelayMs = 0
                    });
                    
                    // Release modifiers in reverse order
                    if (needsAltGr)
                    {
                        events.Add(new MacroEvent
                        {
                            Type = EventType.KeyRelease,
                            KeyCode = InputEventCode.KEY_RIGHTALT,
                            DelayMs = 0
                        });
                    }
                    
                    if (needsShift)
                    {
                        events.Add(new MacroEvent
                        {
                            Type = EventType.KeyRelease,
                            KeyCode = InputEventCode.KEY_LEFTSHIFT,
                            DelayMs = 0
                        });
                    }
                    
                    isFirst = false;
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
            
            // Skip KeyRelease if it was merged with previous KeyPress or TextInput
            if (ev.Type == EventType.KeyRelease && i > 0)
            {
                var prevAction = actions.LastOrDefault();
                if (prevAction?.Type == EditorActionType.KeyPress && prevAction.KeyCode == ev.KeyCode)
                {
                    continue; // Already merged
                }
                if (prevAction?.Type == EditorActionType.TextInput)
                {
                    continue; // Part of text input sequence
                }
            }
            
            // Try to detect and merge consecutive KeyPress events into TextInput
            if (ev.Type == EventType.KeyPress && CanStartTextInputMerge(events, i))
            {
                var (textAction, consumed) = MergeConsecutiveKeyPresses(events, i);
                if (textAction != null && consumed > 0)
                {
                    actions.Add(textAction);
                    i += consumed - 1; // -1 because loop will increment
                    continue;
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
    
    /// <summary>
    /// Determines if the current position can start a TextInput merge.
    /// Requires at least 2 consecutive printable character KeyPress events.
    /// </summary>
    private bool CanStartTextInputMerge(List<MacroEvent> events, int startIndex)
    {
        int printableCount = 0;
        
        for (int i = startIndex; i < events.Count && printableCount < 2; i++)
        {
            var ev = events[i];
            
            // Skip shift key events
            if (IsShiftKey(ev.KeyCode))
                continue;
            
            // Must be KeyPress or KeyRelease
            if (ev.Type != EventType.KeyPress && ev.Type != EventType.KeyRelease)
                break;
            
            // For KeyPress, check if it's a printable character
            if (ev.Type == EventType.KeyPress)
            {
                var c = _keyCodeMapper.GetCharacterForKeyCode(ev.KeyCode, false);
                if (!c.HasValue)
                    break;
                printableCount++;
            }
        }
        
        return printableCount >= 2;
    }
    
    /// <summary>
    /// Merges consecutive KeyPress events into a single TextInput action.
    /// </summary>
    private (EditorAction?, int) MergeConsecutiveKeyPresses(List<MacroEvent> events, int startIndex)
    {
        var text = new System.Text.StringBuilder();
        int consumed = 0;
        bool shiftActive = false;
        int initialDelayMs = 0;
        bool isFirst = true;
        
        for (int i = startIndex; i < events.Count; i++)
        {
            var ev = events[i];
            
            // Track Shift state
            if (IsShiftKey(ev.KeyCode))
            {
                shiftActive = ev.Type == EventType.KeyPress;
                consumed++;
                continue;
            }
            
            // Only process KeyPress (skip KeyRelease)
            if (ev.Type == EventType.KeyRelease)
            {
                consumed++;
                continue;
            }
            
            if (ev.Type != EventType.KeyPress)
                break;
            
            var c = _keyCodeMapper.GetCharacterForKeyCode(ev.KeyCode, shiftActive);
            if (!c.HasValue)
                break;
            
            // Capture delay from first character
            if (isFirst)
            {
                initialDelayMs = ev.DelayMs;
                isFirst = false;
            }
            
            text.Append(c.Value);
            consumed++;
        }
        
        if (text.Length < 2)
            return (null, 0);
        
        return (new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = text.ToString(),
            DelayMs = initialDelayMs
        }, consumed);
    }
    
    private static bool IsShiftKey(int keyCode)
    {
        return keyCode == InputEventCode.KEY_LEFTSHIFT || keyCode == InputEventCode.KEY_RIGHTSHIFT;
    }
    
    private static bool IsScrollButton(MouseButton button)
    {
        return button is MouseButton.ScrollUp or MouseButton.ScrollDown 
            or MouseButton.ScrollLeft or MouseButton.ScrollRight;
    }
}

