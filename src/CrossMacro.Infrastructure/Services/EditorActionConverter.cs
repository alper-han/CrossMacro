using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Converts between EditorAction and MacroEvent/MacroSequence.
/// Handles bidirectional conversion while preserving .macro format compatibility.
/// </summary>
public class EditorActionConverter : IEditorActionConverter
{
    private const int DefaultKeyPressDelayMs = 10;
    
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly RunScriptCompiler _runScriptCompiler;
    
    public EditorActionConverter(IKeyCodeMapper keyCodeMapper)
    {
        _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
        _runScriptCompiler = new RunScriptCompiler(_keyCodeMapper);
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
                    DelayMs = action.DelayMs,
                    CoordinateMode = action.IsAbsolute ? MouseCoordinateMode.Absolute : MouseCoordinateMode.Relative
                });
                break;
                
            case EditorActionType.MouseClick:
                events.Add(new MacroEvent
                {
                    Type = EventType.Click,
                    X = action.UseCurrentPosition ? 0 : action.X,
                    Y = action.UseCurrentPosition ? 0 : action.Y,
                    Button = action.Button,
                    DelayMs = action.DelayMs,
                    UseCurrentPosition = action.UseCurrentPosition,
                    CoordinateMode = GetCoordinateMode(action)
                });
                break;
                
            case EditorActionType.MouseDown:
                events.Add(new MacroEvent
                {
                    Type = EventType.ButtonPress,
                    X = action.UseCurrentPosition ? 0 : action.X,
                    Y = action.UseCurrentPosition ? 0 : action.Y,
                    Button = action.Button,
                    DelayMs = action.DelayMs,
                    UseCurrentPosition = action.UseCurrentPosition,
                    CoordinateMode = GetCoordinateMode(action)
                });
                break;
                
            case EditorActionType.MouseUp:
                events.Add(new MacroEvent
                {
                    Type = EventType.ButtonRelease,
                    X = action.UseCurrentPosition ? 0 : action.X,
                    Y = action.UseCurrentPosition ? 0 : action.Y,
                    Button = action.Button,
                    DelayMs = action.DelayMs,
                    UseCurrentPosition = action.UseCurrentPosition,
                    CoordinateMode = GetCoordinateMode(action)
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
                    DelayMs = action.UseRandomDelay ? 0 : action.DelayMs,
                    HasRandomDelay = action.UseRandomDelay,
                    RandomDelayMinMs = action.UseRandomDelay ? action.RandomDelayMinMs : 0,
                    RandomDelayMaxMs = action.UseRandomDelay ? action.RandomDelayMaxMs : 0
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
                var preservedTextInputEvents = action.GetPreservedTextInputEvents();
                if (preservedTextInputEvents != null)
                {
                    events.AddRange(preservedTextInputEvents.Select(CloneEvent));
                    break;
                }

                bool isFirst = true;
                for (var index = 0; index < action.Text.Length; index++)
                {
                    var c = action.Text[index];
                    if (c == '\r' && index + 1 < action.Text.Length && action.Text[index + 1] == '\n')
                    {
                        index++;
                        AddKeyStroke(events, InputEventCode.KEY_ENTER, ref isFirst, action.DelayMs);
                        continue;
                    }

                    if (TryGetTextInputControlKeyCode(c, out var controlKeyCode))
                    {
                        AddKeyStroke(events, controlKeyCode, ref isFirst, action.DelayMs);
                        continue;
                    }

                    var keyCode = _keyCodeMapper.GetKeyCodeForCharacter(c);
                    if (keyCode == -1) continue; // Skip unmappable characters

                    var needsShift = _keyCodeMapper.RequiresShift(c);
                    var needsAltGr = _keyCodeMapper.RequiresAltGr(c);
                    AddKeyStroke(events, keyCode, ref isFirst, action.DelayMs, needsShift, needsAltGr);
                }
                break;

            case EditorActionType.SetVariable:
            case EditorActionType.IncrementVariable:
            case EditorActionType.DecrementVariable:
            case EditorActionType.RepeatBlockStart:
            case EditorActionType.IfBlockStart:
            case EditorActionType.ElseBlockStart:
            case EditorActionType.WhileBlockStart:
            case EditorActionType.ForBlockStart:
            case EditorActionType.Break:
            case EditorActionType.Continue:
            case EditorActionType.BlockEnd:
            case EditorActionType.RawScriptStep:
                break;
        }
        
        return events;
    }

    private static bool TryGetTextInputControlKeyCode(char character, out int keyCode)
    {
        keyCode = character switch
        {
            '\r' or '\n' => InputEventCode.KEY_ENTER,
            '\t' => InputEventCode.KEY_TAB,
            '\b' => InputEventCode.KEY_BACKSPACE,
            _ => -1
        };

        return keyCode != -1;
    }

    private static void AddKeyStroke(
        ICollection<MacroEvent> events,
        int keyCode,
        ref bool isFirst,
        int initialDelayMs,
        bool needsShift = false,
        bool needsAltGr = false)
    {
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

        events.Add(new MacroEvent
        {
            Type = EventType.KeyPress,
            KeyCode = keyCode,
            DelayMs = isFirst ? initialDelayMs : DefaultKeyPressDelayMs
        });
        events.Add(new MacroEvent
        {
            Type = EventType.KeyRelease,
            KeyCode = keyCode,
            DelayMs = 0
        });

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

    private static MouseCoordinateMode? GetCoordinateMode(EditorAction action)
    {
        if (action.UseCurrentPosition || IsScrollButton(action.Button))
        {
            return null;
        }

        return action.IsAbsolute ? MouseCoordinateMode.Absolute : MouseCoordinateMode.Relative;
    }

    private static MacroEvent CloneEvent(MacroEvent ev)
    {
        return ev;
    }

    private EditorAction CreateKeyAction(EditorActionType type, int keyCode)
    {
        return new EditorAction
        {
            Type = type,
            KeyCode = keyCode,
            KeyName = _keyCodeMapper.GetKeyName(keyCode)
        };
    }
    
    /// <inheritdoc/>
    public EditorAction FromMacroEvent(MacroEvent ev, MacroEvent? nextEvent = null)
    {
        var action = new EditorAction
        {
            DelayMs = ev.DelayMs,
            UseRandomDelay = ev.HasRandomDelay,
            RandomDelayMinMs = ev.RandomDelayMinMs,
            RandomDelayMaxMs = ev.RandomDelayMaxMs
        };
        
        switch (ev.Type)
        {
            case EventType.MouseMove:
                action.Type = EditorActionType.MouseMove;
                action.X = ev.X;
                action.Y = ev.Y;
                if (ev.CoordinateMode.HasValue)
                {
                    action.IsAbsolute = ev.CoordinateMode.Value == MouseCoordinateMode.Absolute;
                }
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
                    action.UseCurrentPosition = ev.UseCurrentPosition;
                    if (ev.CoordinateMode.HasValue)
                    {
                        action.IsAbsolute = ev.CoordinateMode.Value == MouseCoordinateMode.Absolute;
                    }
                }
                break;
                
            case EventType.ButtonPress:
                action.Type = EditorActionType.MouseDown;
                action.X = ev.X;
                action.Y = ev.Y;
                action.Button = ev.Button;
                action.UseCurrentPosition = ev.UseCurrentPosition;
                if (ev.CoordinateMode.HasValue)
                {
                    action.IsAbsolute = ev.CoordinateMode.Value == MouseCoordinateMode.Absolute;
                }
                break;
                
            case EventType.ButtonRelease:
                action.Type = EditorActionType.MouseUp;
                action.X = ev.X;
                action.Y = ev.Y;
                action.Button = ev.Button;
                action.UseCurrentPosition = ev.UseCurrentPosition;
                if (ev.CoordinateMode.HasValue)
                {
                    action.IsAbsolute = ev.CoordinateMode.Value == MouseCoordinateMode.Absolute;
                }
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
                action.KeyName = _keyCodeMapper.GetKeyName(ev.KeyCode);
                break;
                
            case EventType.KeyRelease:
                action.Type = EditorActionType.KeyUp;
                action.KeyCode = ev.KeyCode;
                action.KeyName = _keyCodeMapper.GetKeyName(ev.KeyCode);
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
        var actionList = actions.ToList();
        var hasFlowControlScriptActions = actionList.Any(action => EditorActionScriptClassifier.IsScriptFlowControlAction(action.Type));
        var hasStateScriptActions = actionList.Any(action => EditorActionScriptClassifier.IsScriptStateAction(action.Type));
        var hasOpaqueScriptActions = actionList.Any(action => EditorActionScriptClassifier.IsOpaqueScriptAction(action.Type));
        var hasRuntimeEventActions = actionList.Any(action => EditorActionScriptClassifier.IsRuntimeEventAction(action.Type));
        if (hasFlowControlScriptActions || hasOpaqueScriptActions || (hasStateScriptActions && !hasRuntimeEventActions))
        {
            return CompileScriptBackedSequence(actionList, name);
        }

        var sequence = new MacroSequence
        {
            Name = name,
            IsAbsoluteCoordinates = isAbsolute,
            SkipInitialZeroZero = skipInitialZeroZero,
            CreatedAt = DateTime.UtcNow
        };
        
        long timestamp = 0;
        int pendingDelay = 0;
        bool hasPendingRandomDelay = false;
        int pendingRandomDelayMinMs = 0;
        int pendingRandomDelayMaxMs = 0;
        
        foreach (var action in actionList)
        {
            var events = ToMacroEvents(action);
            var actionStartEventIndex = sequence.Events.Count;
            var actionEventCount = 0;
            
            foreach (var ev in events)
            {
                // Skip None type events but accumulate their delay
                if (ev.Type == EventType.None)
                {
                    pendingDelay += ev.DelayMs;
                    if (ev.HasRandomDelay)
                    {
                        hasPendingRandomDelay = true;
                        pendingRandomDelayMinMs += ev.RandomDelayMinMs;
                        pendingRandomDelayMaxMs += ev.RandomDelayMaxMs;
                    }
                    continue;
                }

                var eventToAdd = ev;
                eventToAdd.DelayMs += pendingDelay;
                if (hasPendingRandomDelay)
                {
                    eventToAdd.HasRandomDelay = true;
                    eventToAdd.RandomDelayMinMs += pendingRandomDelayMinMs;
                    eventToAdd.RandomDelayMaxMs += pendingRandomDelayMaxMs;
                }
                eventToAdd.Timestamp = timestamp;

                timestamp += eventToAdd.DelayMs;
                if (eventToAdd.HasRandomDelay)
                {
                    timestamp += eventToAdd.RandomDelayMinMs;
                }
                pendingDelay = 0;
                hasPendingRandomDelay = false;
                pendingRandomDelayMinMs = 0;
                pendingRandomDelayMaxMs = 0;

                sequence.Events.Add(eventToAdd);
                actionEventCount++;
            }

            if (action.Type == EditorActionType.TextInput && actionEventCount > 0)
            {
                sequence.TextInputBoundaries.Add(new TextInputBoundary(
                    actionStartEventIndex,
                    actionEventCount,
                    action.Text));
            }
        }

        // Preserve trailing delay for looped macros
        if (pendingDelay > 0 || hasPendingRandomDelay)
        {
            sequence.TrailingDelayMs = pendingDelay;
            sequence.HasTrailingRandomDelay = hasPendingRandomDelay;
            sequence.TrailingDelayMinMs = pendingRandomDelayMinMs;
            sequence.TrailingDelayMaxMs = pendingRandomDelayMaxMs;
        }
        
        sequence.CalculateDuration();
        sequence.MouseMoveCount = sequence.Events.Count(e => e.Type == EventType.MouseMove);
        sequence.ClickCount = sequence.Events.Count(e => e.Type != EventType.MouseMove);

        if (hasStateScriptActions)
        {
            sequence.SkipInitialZeroZero = true;
            sequence.ScriptSteps = BuildScriptSteps(actionList)
                .Select(step => step.Step)
                .Where(step => !string.IsNullOrWhiteSpace(step))
                .ToList();
        }
        
        return sequence;
    }

    private MacroSequence CompileScriptBackedSequence(IReadOnlyList<EditorAction> actions, string name)
    {
        var scriptSteps = BuildScriptSteps(actions);
        var compileResult = _runScriptCompiler.Compile(scriptSteps);
        if (!compileResult.Success || compileResult.Sequence == null)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(compileResult.ErrorMessage)
                ? "Script compilation failed."
                : compileResult.ErrorMessage);
        }

        var sequence = compileResult.Sequence;
        sequence.Name = name;
        sequence.CreatedAt = DateTime.UtcNow;
        sequence.ScriptSteps = scriptSteps
            .Select(step => step.Step)
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .ToList();

        if (sequence.Events.Count > 0 && (compileResult.InitialDelayMs > 0 || compileResult.InitialHasRandomDelay))
        {
            var firstEvent = sequence.Events[0];
            firstEvent.DelayMs += compileResult.InitialDelayMs;
            if (compileResult.InitialHasRandomDelay)
            {
                firstEvent.HasRandomDelay = true;
                firstEvent.RandomDelayMinMs += compileResult.InitialRandomDelayMinMs;
                firstEvent.RandomDelayMaxMs += compileResult.InitialRandomDelayMaxMs;
            }

            sequence.Events[0] = firstEvent;
        }

        RecalculateTimestamps(sequence);
        sequence.CalculateDuration();
        sequence.MouseMoveCount = sequence.Events.Count(e => e.Type == EventType.MouseMove);
        sequence.ClickCount = sequence.Events.Count(e => e.Type != EventType.MouseMove);
        return sequence;
    }

    private static void RecalculateTimestamps(MacroSequence sequence)
    {
        long timestamp = 0;
        for (var i = 0; i < sequence.Events.Count; i++)
        {
            var ev = sequence.Events[i];
            ev.Timestamp = timestamp;
            timestamp += ev.DelayMs;
            if (ev.HasRandomDelay)
            {
                timestamp += ev.RandomDelayMinMs;
            }

            sequence.Events[i] = ev;
        }
    }

    private List<RunScriptStep> BuildScriptSteps(IReadOnlyList<EditorAction> actions)
    {
        var steps = new List<RunScriptStep>();
        var sourceIndex = 0;

        foreach (var action in actions)
        {
            sourceIndex++;
            var actionSteps = ConvertActionToScriptSteps(action)
                .Where(step => !string.IsNullOrWhiteSpace(step))
                .ToList();

            if (CanSkipLeadingAbsoluteMove(action, steps, actionSteps))
            {
                actionSteps.RemoveAt(0);
            }

            foreach (var step in actionSteps)
            {
                steps.Add(new RunScriptStep(step, null, sourceIndex));
            }
        }

        return steps;
    }

    private static IEnumerable<string> ConvertActionToScriptSteps(EditorAction action)
    {
        switch (action.Type)
        {
            case EditorActionType.MouseMove:
                yield return $"move {(action.IsAbsolute ? "abs" : "rel")} {action.X} {action.Y}";
                yield break;

            case EditorActionType.MouseClick:
                if (action.UseCurrentPosition)
                {
                    yield return $"click {RunScriptSyntax.CurrentPositionToken} {ToButtonToken(action.Button)}";
                }
                else
                {
                    yield return $"move {(action.IsAbsolute ? "abs" : "rel")} {action.X} {action.Y}";
                    yield return $"click {ToButtonToken(action.Button)}";
                }

                yield break;

            case EditorActionType.MouseDown:
                if (!action.UseCurrentPosition)
                {
                    yield return $"move {(action.IsAbsolute ? "abs" : "rel")} {action.X} {action.Y}";
                }

                yield return action.UseCurrentPosition
                    ? $"down {RunScriptSyntax.CurrentPositionToken} {ToButtonToken(action.Button)}"
                    : $"down {ToButtonToken(action.Button)}";
                yield break;

            case EditorActionType.MouseUp:
                if (!action.UseCurrentPosition)
                {
                    yield return $"move {(action.IsAbsolute ? "abs" : "rel")} {action.X} {action.Y}";
                }

                yield return action.UseCurrentPosition
                    ? $"up {RunScriptSyntax.CurrentPositionToken} {ToButtonToken(action.Button)}"
                    : $"up {ToButtonToken(action.Button)}";
                yield break;

            case EditorActionType.KeyPress:
                yield return $"tap {action.KeyCode}";
                yield break;

            case EditorActionType.KeyDown:
                yield return $"key down {action.KeyCode}";
                yield break;

            case EditorActionType.KeyUp:
                yield return $"key up {action.KeyCode}";
                yield break;

            case EditorActionType.Delay:
                yield return action.UseRandomDelay
                    ? $"delay random {action.RandomDelayMinMs} {action.RandomDelayMaxMs}"
                    : $"delay {action.DelayMs}";
                yield break;

            case EditorActionType.ScrollVertical:
                yield return action.ScrollAmount > 0
                    ? $"scroll up {Math.Abs(action.ScrollAmount)}"
                    : $"scroll down {Math.Abs(action.ScrollAmount)}";
                yield break;

            case EditorActionType.ScrollHorizontal:
                yield return action.ScrollAmount > 0
                    ? $"scroll right {Math.Abs(action.ScrollAmount)}"
                    : $"scroll left {Math.Abs(action.ScrollAmount)}";
                yield break;

            case EditorActionType.TextInput:
                yield return $"type {EditorActionScriptTokens.EscapeLiteralDollar(action.Text)}";
                yield break;

            case EditorActionType.SetVariable:
                yield return BuildSetStep(action);
                yield break;

            case EditorActionType.IncrementVariable:
                yield return BuildIncrementStep(action);
                yield break;

            case EditorActionType.DecrementVariable:
                yield return BuildDecrementStep(action);
                yield break;

            case EditorActionType.RepeatBlockStart:
                yield return BuildRepeatStep(action);
                yield break;

            case EditorActionType.IfBlockStart:
                yield return BuildConditionStep("if", action);
                yield break;

            case EditorActionType.ElseBlockStart:
                yield return RunScriptSyntax.ElseBlockHeader;
                yield break;

            case EditorActionType.WhileBlockStart:
                yield return BuildConditionStep("while", action);
                yield break;

            case EditorActionType.ForBlockStart:
                yield return BuildForStep(action);
                yield break;

            case EditorActionType.PixelColor:
                yield return BuildPixelColorStep(action);
                yield break;

            case EditorActionType.WaitColor:
                yield return BuildWaitColorStep(action);
                yield break;

            case EditorActionType.PixelSearch:
                yield return BuildPixelSearchStep(action);
                yield break;

            case EditorActionType.Break:
                yield return RunScriptSyntax.BreakCommand;
                yield break;

            case EditorActionType.Continue:
                yield return RunScriptSyntax.ContinueCommand;
                yield break;

            case EditorActionType.BlockEnd:
                yield return RunScriptSyntax.BlockEndToken;
                yield break;

            case EditorActionType.RawScriptStep:
                yield return action.Text;
                yield break;

            default:
                yield break;
        }
    }

    private static string BuildPixelColorStep(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);
        var variableName = payload.NormalizeColorVariableToken();
        return payload.IsAbsolute
            ? $"pixelcolor {payload.ScreenX} {payload.ScreenY} {variableName}"
            : $"pixelcolor rel {payload.ScreenX} {payload.ScreenY} {variableName}";
    }

    private static string BuildWaitColorStep(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);
        var resultVariableName = payload.NormalizeColorVariableToken();
        return $"waitcolor {payload.ScreenX} {payload.ScreenY} {payload.FormatTargetColorToken()} {payload.ScreenTimeoutMs} {resultVariableName}";
    }

    private static string BuildPixelSearchStep(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);
        var foundVariableName = payload.NormalizeFoundVariableToken();
        var xVariableName = payload.NormalizeFoundXVariableToken();
        var yVariableName = payload.NormalizeFoundYVariableToken();
        return $"pixelsearch {payload.ScreenLeft} {payload.ScreenTop} {payload.ScreenRight} {payload.ScreenBottom} {payload.FormatTargetColorToken()} {foundVariableName} {xVariableName} {yVariableName} tolerance {payload.ScreenTolerance}";
    }

    private static EditorActionScreenReadingPayload GetScreenReadingPayload(EditorAction action)
    {
        if (!action.TryGetScreenReadingPayload(out var payload))
        {
            throw new InvalidOperationException("Action type does not contain a screen-reading payload.");
        }

        return payload;
    }

    private static string ToButtonToken(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => "left",
            MouseButton.Right => "right",
            MouseButton.Middle => "middle",
            MouseButton.Side1 => "side1",
            MouseButton.Side2 => "side2",
            _ => "left"
        };
    }

    private static string BuildSetStep(EditorAction action)
    {
        if (ShouldSerializeLegacySetText(action))
        {
            return $"set {action.Text}";
        }

        var name = EditorActionScriptTokens.NormalizeVariableToken(action.ScriptVariableName);
        var value = EditorActionScriptTokens.FormatSetValueToken(action.ScriptValueType, action.ScriptValue);

        if (action.ScriptValueType == ScriptValueType.Text
            && value.Contains('=', StringComparison.Ordinal))
        {
            return $"set {name}={value}";
        }

        return $"set {name} {value}";
    }

    private static string BuildIncrementStep(EditorAction action)
    {
        if (ShouldSerializeLegacyNumericUpdateText(action))
        {
            return $"inc {action.Text}";
        }

        var variableName = EditorActionScriptTokens.NormalizeVariableToken(action.ScriptVariableName);
        var amountToken = BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue);
        return $"inc {variableName} {amountToken}";
    }

    private static string BuildDecrementStep(EditorAction action)
    {
        if (ShouldSerializeLegacyNumericUpdateText(action))
        {
            return $"dec {action.Text}";
        }

        var variableName = EditorActionScriptTokens.NormalizeVariableToken(action.ScriptVariableName);
        var amountToken = BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue);
        return $"dec {variableName} {amountToken}";
    }

    private static string BuildRepeatStep(EditorAction action)
    {
        if (ShouldSerializeLegacyRepeatText(action))
        {
            return $"repeat {action.Text} {{";
        }

        var countToken = BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue);
        return $"repeat {countToken} {{";
    }

    private static string BuildConditionStep(string keyword, EditorAction action)
    {
        if (ShouldSerializeLegacyConditionText(action))
        {
            return $"{keyword} {action.Text} {{";
        }

        var left = BuildOperandToken(action.ScriptLeftOperandType, action.ScriptLeftOperand);
        var op = EditorActionScriptTokens.ToOperatorToken(action.ScriptConditionOperator);
        var right = BuildOperandToken(action.ScriptRightOperandType, action.ScriptRightOperand);
        return $"{keyword} {left} {op} {right} {{";
    }

    private static string BuildForStep(EditorAction action)
    {
        if (ShouldSerializeLegacyForText(action))
        {
            return $"for {action.Text} {{";
        }

        var variableName = EditorActionScriptTokens.NormalizeVariableToken(action.ForVariableName);
        var start = BuildNumericToken(action.ForStartType, action.ForStartValue);
        var end = BuildNumericToken(action.ForEndType, action.ForEndValue);
        if (!action.ForHasStep)
        {
            return $"for {variableName} from {start} to {end} {{";
        }

        var step = BuildNumericToken(action.ForStepType, action.ForStepValue);
        return $"for {variableName} from {start} to {end} step {step} {{";
    }

    private static string BuildNumericToken(ScriptNumericSourceType sourceType, string value)
    {
        return EditorActionScriptTokens.FormatNumericToken(sourceType, value, defaultValue: string.Empty);
    }

    private static string BuildOperandToken(ScriptOperandType operandType, string value)
    {
        return EditorActionScriptTokens.FormatOperandToken(operandType, value);
    }

    private static bool ShouldSerializeLegacySetText(EditorAction action)
    {
        return action.PreferLegacyScriptText
            && !string.IsNullOrWhiteSpace(action.Text);
    }

    private static bool ShouldSerializeLegacyNumericUpdateText(EditorAction action)
    {
        return action.PreferLegacyScriptText
            && !string.IsNullOrWhiteSpace(action.Text);
    }

    private static bool ShouldSerializeLegacyRepeatText(EditorAction action)
    {
        return action.PreferLegacyScriptText
            && !string.IsNullOrWhiteSpace(action.Text);
    }

    private static bool ShouldSerializeLegacyConditionText(EditorAction action)
    {
        return action.PreferLegacyScriptText
            && !string.IsNullOrWhiteSpace(action.Text);
    }

    private static bool ShouldSerializeLegacyForText(EditorAction action)
    {
        return action.PreferLegacyScriptText
            && !string.IsNullOrWhiteSpace(action.Text);
    }

    private static bool CanSkipLeadingAbsoluteMove(
        EditorAction action,
        IReadOnlyList<RunScriptStep> existingSteps,
        IReadOnlyList<string> actionSteps)
    {
        if (action.Type is not (EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp)
            || action.UseCurrentPosition
            || !action.IsAbsolute
            || existingSteps.Count == 0
            || actionSteps.Count == 0)
        {
            return false;
        }

        if (!TryParseMoveStep(existingSteps[^1].Step, out var previousIsAbsolute, out var previousX, out var previousY)
            || !previousIsAbsolute)
        {
            return false;
        }

        if (!TryParseMoveStep(actionSteps[0], out var currentIsAbsolute, out var currentX, out var currentY)
            || !currentIsAbsolute)
        {
            return false;
        }

        return previousX == currentX && previousY == currentY;
    }
    
    /// <inheritdoc/>
    public List<EditorAction> FromMacroSequence(MacroSequence sequence)
    {
        return FromMacroSequenceWithDiagnostics(sequence).Actions.ToList();
    }

    /// <inheritdoc/>
    public EditorActionRestoreResult FromMacroSequenceWithDiagnostics(MacroSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        if (TryRestoreActionsFromScriptSteps(sequence.ScriptSteps, out var scriptActions, out var warnings))
        {
            return new EditorActionRestoreResult(scriptActions, warnings, restoredFromScriptSteps: true);
        }

        var eventActions = RestoreActionsFromEvents(sequence);
        return new EditorActionRestoreResult(
            eventActions,
            Array.Empty<EditorActionRestoreWarning>(),
            restoredFromScriptSteps: false);
    }

    private List<EditorAction> RestoreActionsFromEvents(MacroSequence sequence)
    {
        var actions = new List<EditorAction>();
        var events = sequence.Events;
        var useLegacyCurrentPositionInterpretation = MacroPositionSemantics.IsLegacyCurrentPositionMacro(sequence);
        var textInputBoundaries = CreateTextInputBoundaryLookup(sequence);
        
        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            var nextEvent = i + 1 < events.Count ? events[i + 1] : (MacroEvent?)null;

            if (textInputBoundaries.TryGetValue(i, out var textInputBoundary))
            {
                AppendDelayActions(
                    actions,
                    ev.DelayMs,
                    ev.HasRandomDelay,
                    ev.RandomDelayMinMs,
                    ev.RandomDelayMaxMs);
                var textInputAction = new EditorAction
                {
                    Type = EditorActionType.TextInput,
                    Text = textInputBoundary.Text
                };
                textInputAction.PreserveTextInputEvents(CopyBoundaryEventsWithoutLeadingDelay(
                    events,
                    textInputBoundary.StartEventIndex,
                    textInputBoundary.EventCount));
                actions.Add(textInputAction);
                i += textInputBoundary.EventCount - 1;
                continue;
            }
            
            // Skip KeyRelease if it was merged with previous KeyPress or TextInput
            if (ev.Type == EventType.KeyRelease && i > 0)
            {
                var prevAction = actions.LastOrDefault();
                if (prevAction?.Type == EditorActionType.KeyPress && prevAction.KeyCode == ev.KeyCode)
                {
                    continue; // Already merged
                }
            }
            
            var action = FromMacroEvent(ev, nextEvent);
            
            // Set IsAbsolute from event-level mode, falling back to legacy sequence metadata.
            if (action.Type is EditorActionType.MouseMove
                or EditorActionType.MouseClick
                or EditorActionType.MouseDown
                or EditorActionType.MouseUp)
            {
                if ((action.Type is EditorActionType.MouseClick
                    or EditorActionType.MouseDown
                    or EditorActionType.MouseUp)
                    && MacroPositionSemantics.UsesCurrentPosition(ev, useLegacyCurrentPositionInterpretation))
                {
                    action.UseCurrentPosition = true;
                    action.IsAbsolute = false;
                    action.X = 0;
                    action.Y = 0;
                }
                else
                {
                    action.IsAbsolute = MacroPositionSemantics.ResolveCoordinateMode(ev, sequence.IsAbsoluteCoordinates)
                        == MouseCoordinateMode.Absolute;
                }
            }

            if (action.Type == EditorActionType.Delay)
            {
                if (action.DelayMs > 0 || action.UseRandomDelay)
                {
                    actions.Add(action);
                }
                continue;
            }

            AppendDelayActions(
                actions,
                action.DelayMs,
                action.UseRandomDelay,
                action.RandomDelayMinMs,
                action.RandomDelayMaxMs);
            action.DelayMs = 0;
            action.UseRandomDelay = false;
            action.RandomDelayMinMs = 0;
            action.RandomDelayMaxMs = 0;
            actions.Add(action);
        }

        // Add trailing delay as Delay action(s) if present.
        AppendDelayActions(
            actions,
            sequence.TrailingDelayMs,
            sequence.HasTrailingRandomDelay,
            sequence.TrailingDelayMinMs,
            sequence.TrailingDelayMaxMs);

        return actions;
    }

    private IReadOnlyDictionary<int, TextInputBoundary> CreateTextInputBoundaryLookup(MacroSequence sequence)
    {
        if (sequence.TextInputBoundaries.Count == 0 || sequence.Events.Count == 0)
        {
            return new Dictionary<int, TextInputBoundary>();
        }

        var boundaries = sequence.TextInputBoundaries
            .OrderBy(boundary => boundary.StartEventIndex)
            .ToList();
        var lookup = new Dictionary<int, TextInputBoundary>();
        var previousEndExclusive = 0;

        foreach (var boundary in boundaries)
        {
            if (boundary.StartEventIndex < previousEndExclusive
                || boundary.EventCount <= 0
                || boundary.StartEventIndex < 0
                || boundary.StartEventIndex + boundary.EventCount > sequence.Events.Count
                || !BoundaryMatchesTextInputEvents(sequence.Events, boundary))
            {
                return new Dictionary<int, TextInputBoundary>();
            }

            lookup.Add(boundary.StartEventIndex, boundary);
            previousEndExclusive = boundary.StartEventIndex + boundary.EventCount;
        }

        return lookup;
    }

    private bool BoundaryMatchesTextInputEvents(IReadOnlyList<MacroEvent> events, TextInputBoundary boundary)
    {
        var expectedEvents = ToMacroEvents(new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = boundary.Text
        });

        if (expectedEvents.Count != boundary.EventCount)
        {
            return false;
        }

        for (var offset = 0; offset < boundary.EventCount; offset++)
        {
            var actual = events[boundary.StartEventIndex + offset];
            var expected = expectedEvents[offset];
            if (actual.Type is not (EventType.KeyPress or EventType.KeyRelease)
                || actual.Type != expected.Type
                || actual.KeyCode != expected.KeyCode)
            {
                return false;
            }
        }

        return true;
    }

    private static List<MacroEvent> CopyBoundaryEventsWithoutLeadingDelay(
        IReadOnlyList<MacroEvent> events,
        int startEventIndex,
        int eventCount)
    {
        var preserved = new List<MacroEvent>(eventCount);
        for (var offset = 0; offset < eventCount; offset++)
        {
            var ev = events[startEventIndex + offset];
            if (offset == 0)
            {
                ev.DelayMs = 0;
                ev.HasRandomDelay = false;
                ev.RandomDelayMinMs = 0;
                ev.RandomDelayMaxMs = 0;
            }

            preserved.Add(ev);
        }

        return preserved;
    }

    private bool TryRestoreActionsFromScriptSteps(
        IReadOnlyList<string>? scriptSteps,
        out List<EditorAction> actions,
        out List<EditorActionRestoreWarning> warnings)
    {
        actions = new List<EditorAction>();
        warnings = new List<EditorActionRestoreWarning>();
        if (scriptSteps == null || scriptSteps.Count == 0)
        {
            return false;
        }

        var hasAbsoluteCursorPosition = false;
        var absoluteCursorX = 0;
        var absoluteCursorY = 0;
        MouseCoordinateMode? currentMoveMode = null;

        for (var index = 0; index < scriptSteps.Count; index++)
        {
            var rawStep = scriptSteps[index];
            if (string.IsNullOrWhiteSpace(rawStep))
            {
                continue;
            }

            var step = rawStep.Trim();
            var stepForType = rawStep.TrimStart();

            if (TryParseMoveStep(step, out var isAbsoluteMove, out var moveX, out var moveY))
            {
                currentMoveMode = isAbsoluteMove ? MouseCoordinateMode.Absolute : MouseCoordinateMode.Relative;
                if (isAbsoluteMove)
                {
                    hasAbsoluteCursorPosition = true;
                    absoluteCursorX = moveX;
                    absoluteCursorY = moveY;
                }
                else
                {
                    hasAbsoluteCursorPosition = false;
                }

                actions.Add(new EditorAction
                {
                    Type = EditorActionType.MouseMove,
                    IsAbsolute = isAbsoluteMove,
                    X = moveX,
                    Y = moveY
                });
                continue;
            }

            if (TryParseButtonStep(
                step,
                out var currentButtonKeyword,
                out var currentButton,
                out var isCurrentPositionExplicit))
            {
                if (isCurrentPositionExplicit)
                {
                    actions.Add(CreateCurrentPositionButtonAction(currentButtonKeyword, currentButton));
                    continue;
                }

                if (currentMoveMode == MouseCoordinateMode.Absolute && hasAbsoluteCursorPosition)
                {
                    actions.Add(CreatePositionedButtonAction(
                        currentButtonKeyword,
                        currentButton,
                        isAbsolute: true,
                        absoluteCursorX,
                        absoluteCursorY));
                    continue;
                }

                if (currentMoveMode == MouseCoordinateMode.Relative)
                {
                    actions.Add(CreatePositionedButtonAction(
                        currentButtonKeyword,
                        currentButton,
                        isAbsolute: false,
                        0,
                        0));
                    continue;
                }

                actions.Add(CreateCurrentPositionButtonAction(currentButtonKeyword, currentButton));
                continue;
            }

            if (TryParseTapStep(step, out var tapKeyCode))
            {
                actions.Add(CreateKeyAction(EditorActionType.KeyPress, tapKeyCode));
                continue;
            }

            if (TryParseKeyStep(step, out var keyActionType, out var keyCode))
            {
                actions.Add(CreateKeyAction(keyActionType, keyCode));
                continue;
            }

            if (TryParseDelayStep(step, out var useRandomDelay, out var fixedDelay, out var randomMin, out var randomMax))
            {
                actions.Add(new EditorAction
                {
                    Type = EditorActionType.Delay,
                    UseRandomDelay = useRandomDelay,
                    DelayMs = useRandomDelay ? 0 : fixedDelay,
                    RandomDelayMinMs = useRandomDelay ? randomMin : 0,
                    RandomDelayMaxMs = useRandomDelay ? randomMax : 0
                });
                continue;
            }

            if (TryParseScrollStep(step, out var scrollActionType, out var scrollAmount))
            {
                actions.Add(new EditorAction
                {
                    Type = scrollActionType,
                    ScrollAmount = scrollAmount
                });
                continue;
            }

            if (TryParseTypeStep(stepForType, out var text))
            {
                actions.Add(new EditorAction
                {
                    Type = EditorActionType.TextInput,
                    Text = EditorActionScriptTokens.UnescapeLiteralDollar(text)
                });
                continue;
            }

            if (TryParseSetStep(step, out var setAction))
            {
                actions.Add(setAction);
                continue;
            }

            if (TryParseScreenReadingStep(step, out var screenReadingAction))
            {
                actions.Add(screenReadingAction);
                continue;
            }

            if (TryParseIncDecStep(step, "inc", EditorActionType.IncrementVariable, out var incrementAction))
            {
                actions.Add(incrementAction);
                continue;
            }

            if (TryParseIncDecStep(step, "dec", EditorActionType.DecrementVariable, out var decrementAction))
            {
                actions.Add(decrementAction);
                continue;
            }

            if (TryParseRepeatStep(step, out var repeatAction))
            {
                actions.Add(repeatAction);
                continue;
            }

            if (TryParseConditionStep(step, "if", EditorActionType.IfBlockStart, out var ifAction))
            {
                actions.Add(ifAction);
                continue;
            }

            if (TryParseConditionStep(step, "while", EditorActionType.WhileBlockStart, out var whileAction))
            {
                actions.Add(whileAction);
                continue;
            }

            if (TryParseForStep(step, out var forAction))
            {
                actions.Add(forAction);
                continue;
            }

            if (RunScriptSyntax.IsElseHeader(step))
            {
                actions.Add(new EditorAction { Type = EditorActionType.ElseBlockStart });
                continue;
            }

            if (RunScriptSyntax.IsBreakCommand(step))
            {
                actions.Add(new EditorAction { Type = EditorActionType.Break });
                continue;
            }

            if (RunScriptSyntax.IsContinueCommand(step))
            {
                actions.Add(new EditorAction { Type = EditorActionType.Continue });
                continue;
            }

            if (RunScriptSyntax.IsBlockEndToken(step))
            {
                actions.Add(new EditorAction { Type = EditorActionType.BlockEnd });
                continue;
            }

            warnings.Add(new EditorActionRestoreWarning(
                index + 1,
                step,
                "Unsupported step restored as raw script text."));
            actions.Add(CreateRawScriptStepAction(step));
        }

        return actions.Count > 0;
    }

    private static bool TryParseMoveStep(string step, out bool isAbsolute, out int x, out int y)
    {
        isAbsolute = false;
        x = 0;
        y = 0;

        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length != 4 || !tokens[0].Equals("move", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!tokens[1].Equals("abs", StringComparison.OrdinalIgnoreCase)
            && !tokens[1].Equals("absolute", StringComparison.OrdinalIgnoreCase)
            && !tokens[1].Equals("rel", StringComparison.OrdinalIgnoreCase)
            && !tokens[1].Equals("relative", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
            || !int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
        {
            return false;
        }

        isAbsolute = tokens[1].Equals("abs", StringComparison.OrdinalIgnoreCase)
            || tokens[1].Equals("absolute", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static bool TryParseButtonStep(
        string? rawStep,
        out string keyword,
        out MouseButton button,
        out bool isCurrentPositionExplicit)
    {
        keyword = string.Empty;
        button = MouseButton.Left;
        isCurrentPositionExplicit = false;
        if (string.IsNullOrWhiteSpace(rawStep))
        {
            return false;
        }

        var step = rawStep.Trim();
        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 2 || tokens.Length > 3)
        {
            return false;
        }

        if (!tokens[0].Equals("click", StringComparison.OrdinalIgnoreCase)
            && !tokens[0].Equals("down", StringComparison.OrdinalIgnoreCase)
            && !tokens[0].Equals("up", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (tokens.Length == 3)
        {
            if (!RunScriptSyntax.IsCurrentPositionToken(tokens[1]))
            {
                return false;
            }

            if (!TryParseButtonToken(tokens[2], out button))
            {
                return false;
            }

            isCurrentPositionExplicit = true;
        }
        else if (!TryParseButtonToken(tokens[1], out button))
        {
            return false;
        }

        keyword = tokens[0].ToLowerInvariant();
        return true;
    }

    private static bool TryParseButtonToken(string token, out MouseButton button)
    {
        button = token.ToLowerInvariant() switch
        {
            "left" or "l" => MouseButton.Left,
            "right" or "r" => MouseButton.Right,
            "middle" or "m" => MouseButton.Middle,
            "side1" or "side" or "back" => MouseButton.Side1,
            "side2" or "extra" or "forward" => MouseButton.Side2,
            _ => MouseButton.None
        };

        return button != MouseButton.None;
    }

    private static EditorAction CreatePositionedButtonAction(string keyword, MouseButton button, bool isAbsolute, int x, int y)
    {
        var actionType = keyword switch
        {
            "click" => EditorActionType.MouseClick,
            "down" => EditorActionType.MouseDown,
            "up" => EditorActionType.MouseUp,
            _ => EditorActionType.MouseClick
        };

        return new EditorAction
        {
            Type = actionType,
            Button = button,
            IsAbsolute = isAbsolute,
            X = x,
            Y = y,
            UseCurrentPosition = false
        };
    }

    private static EditorAction CreateCurrentPositionButtonAction(string keyword, MouseButton button)
    {
        var actionType = keyword switch
        {
            "click" => EditorActionType.MouseClick,
            "down" => EditorActionType.MouseDown,
            "up" => EditorActionType.MouseUp,
            _ => EditorActionType.MouseClick
        };

        return new EditorAction
        {
            Type = actionType,
            Button = button,
            IsAbsolute = false,
            X = 0,
            Y = 0,
            UseCurrentPosition = true
        };
    }

    private static EditorAction CreateRawScriptStepAction(string step)
    {
        return new EditorAction
        {
            Type = EditorActionType.RawScriptStep,
            Text = step
        };
    }

    private bool TryParseTapStep(string step, out int keyCode)
    {
        keyCode = 0;
        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length != 2 || !tokens[0].Equals("tap", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var keyToken = tokens[1];
        if (keyToken.Contains('+', StringComparison.Ordinal))
        {
            return false;
        }

        return TryResolveKeyCodeToken(keyToken, out keyCode);
    }

    private bool TryParseKeyStep(string step, out EditorActionType actionType, out int keyCode)
    {
        actionType = EditorActionType.KeyDown;
        keyCode = 0;

        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length != 3 || !tokens[0].Equals("key", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!tokens[1].Equals("down", StringComparison.OrdinalIgnoreCase)
            && !tokens[1].Equals("up", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var keyToken = tokens[2];
        if (keyToken.Contains('+', StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryResolveKeyCodeToken(keyToken, out keyCode))
        {
            return false;
        }

        actionType = tokens[1].Equals("down", StringComparison.OrdinalIgnoreCase)
            ? EditorActionType.KeyDown
            : EditorActionType.KeyUp;
        return true;
    }

    private bool TryResolveKeyCodeToken(string token, out int keyCode)
    {
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out keyCode))
        {
            return keyCode > 0;
        }

        keyCode = _keyCodeMapper.GetKeyCode(token);
        return keyCode > 0;
    }

    private static bool TryParseDelayStep(
        string step,
        out bool useRandomDelay,
        out int fixedDelayMs,
        out int randomMinDelayMs,
        out int randomMaxDelayMs)
    {
        useRandomDelay = false;
        fixedDelayMs = 0;
        randomMinDelayMs = 0;
        randomMaxDelayMs = 0;

        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 2 || !tokens[0].Equals("delay", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (tokens.Length == 2)
        {
            return int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out fixedDelayMs);
        }

        if (tokens.Length == 4 && tokens[1].Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out randomMinDelayMs)
                || !int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out randomMaxDelayMs))
            {
                return false;
            }

            useRandomDelay = true;
            return true;
        }

        if (tokens.Length == 3 && tokens[1].Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            var rangeTokens = tokens[2].Split("..", 2, StringSplitOptions.TrimEntries);
            if (rangeTokens.Length != 2
                || !int.TryParse(rangeTokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out randomMinDelayMs)
                || !int.TryParse(rangeTokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out randomMaxDelayMs))
            {
                return false;
            }

            useRandomDelay = true;
            return true;
        }

        return false;
    }

    private static bool TryParseScrollStep(string step, out EditorActionType actionType, out int amount)
    {
        actionType = EditorActionType.ScrollVertical;
        amount = 0;

        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if ((tokens.Length != 2 && tokens.Length != 3) || !tokens[0].Equals("scroll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parsedAmount = 1;
        if (tokens.Length == 3
            && !int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedAmount))
        {
            return false;
        }

        if (parsedAmount <= 0)
        {
            return false;
        }

        switch (tokens[1].ToLowerInvariant())
        {
            case "up":
                actionType = EditorActionType.ScrollVertical;
                amount = parsedAmount;
                return true;
            case "down":
                actionType = EditorActionType.ScrollVertical;
                amount = -parsedAmount;
                return true;
            case "right":
                actionType = EditorActionType.ScrollHorizontal;
                amount = parsedAmount;
                return true;
            case "left":
                actionType = EditorActionType.ScrollHorizontal;
                amount = -parsedAmount;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseTypeStep(string step, out string text)
    {
        text = string.Empty;
        if (!step.StartsWith("type", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (step.Length == 4)
        {
            return false;
        }

        if (!step.StartsWith("type ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        text = step[5..];
        return true;
    }

    private static bool TryParseSetStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!step.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[4..].Trim();
        if (payload.Length == 0)
        {
            return false;
        }

        if (TryParseStructuredSetPayload(payload, out var variableName, out var valueType, out var value))
        {
            action = new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = variableName,
                ScriptValueType = valueType,
                ScriptValue = value
            };
            return true;
        }

        action = new EditorAction
        {
            Type = EditorActionType.SetVariable,
            Text = payload
        };
        return true;
    }

    private static bool TryParseStructuredSetPayload(
        string payload,
        out string variableName,
        out ScriptValueType valueType,
        out string value)
    {
        variableName = string.Empty;
        valueType = ScriptValueType.Text;
        value = string.Empty;

        var equalIndex = payload.IndexOf('=');
        string rawName;
        string rawValue;
        if (equalIndex > 0)
        {
            rawName = payload[..equalIndex].Trim();
            rawValue = payload[(equalIndex + 1)..].Trim();
        }
        else
        {
            var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            rawName = parts[0];
            rawValue = parts[1].Trim();
        }

        if (!TryNormalizeVariableName(rawName, out variableName) || string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        if (!TryInferSetValue(rawValue, out valueType, out value))
        {
            return false;
        }

        return true;
    }

    private static bool TryParseScreenReadingStep(string step, out EditorAction action)
    {
        return TryParsePixelColorStep(step, out action)
            || TryParseWaitColorStep(step, out action)
            || TryParsePixelSearchStep(step, out action);
    }

    private static bool TryParsePixelColorStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptScreenReadingStepParser.TryParseCommand(step, out var command, out var tokens)
            || command != RunScriptScreenReadingCommand.PixelColor)
        {
            return false;
        }

        if (tokens.Length == 4)
        {
            if (!TryParseInteger(tokens[1], out var x)
                || !TryParseInteger(tokens[2], out var y)
                || !TryNormalizeVariableName(tokens[3], out var variableName))
            {
                return false;
            }

            action = new EditorAction();
            action.ApplyScreenReadingPayload(EditorActionScreenReadingPayload.ForPixelColor(true, x, y, variableName));
            return true;
        }

        if (tokens.Length == 5 && tokens[1].Equals("rel", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseInteger(tokens[2], out var x)
                || !TryParseInteger(tokens[3], out var y)
                || !TryNormalizeVariableName(tokens[4], out var variableName))
            {
                return false;
            }

            action = new EditorAction();
            action.ApplyScreenReadingPayload(EditorActionScreenReadingPayload.ForPixelColor(false, x, y, variableName));
            return true;
        }

        return false;
    }

    private static bool TryParseWaitColorStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptScreenReadingStepParser.TryParseCommand(step, out var command, out var tokens)
            || command != RunScriptScreenReadingCommand.WaitColor
            || tokens.Length is not (5 or 6))
        {
            return false;
        }

        if (!TryParseInteger(tokens[1], out var x)
            || !TryParseInteger(tokens[2], out var y)
            || !TryParseTargetColorToken(tokens[3], out var colorSource, out var colorHex, out var targetColorVariableName)
            || !TryParseInteger(tokens[4], out var timeoutMs))
        {
            return false;
        }

        var variableName = tokens.Length == 6 && TryNormalizeVariableName(tokens[5], out var resultVariableName)
            ? resultVariableName
            : EditorActionScreenReadingPayload.DefaultColorVariableName;
        action = new EditorAction();
        action.ApplyScreenReadingPayload(EditorActionScreenReadingPayload.ForWaitColor(x, y, colorHex, timeoutMs, variableName));
        action.ScreenTargetColorSource = colorSource;
        action.ScreenTargetColorVariableName = targetColorVariableName;
        return true;
    }

    private static bool TryParsePixelSearchStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptScreenReadingStepParser.TryParseCommand(step, out var command, out var tokens)
            || command != RunScriptScreenReadingCommand.PixelSearch
            || tokens.Length is not (8 or 9 or 10 or 11))
        {
            return false;
        }

        var hasFoundVariable = tokens.Length is 9 or 11;
        var xVariableIndex = hasFoundVariable ? 7 : 6;
        var yVariableIndex = hasFoundVariable ? 8 : 7;
        var toleranceKeywordIndex = hasFoundVariable ? 9 : 8;

        if (!TryParseInteger(tokens[1], out var x1)
            || !TryParseInteger(tokens[2], out var y1)
            || !TryParseInteger(tokens[3], out var x2)
            || !TryParseInteger(tokens[4], out var y2)
            || !TryParseTargetColorToken(tokens[5], out var colorSource, out var colorHex, out var targetColorVariableName)
            || (hasFoundVariable && !TryNormalizeVariableName(tokens[6], out _))
            || !TryNormalizeVariableName(tokens[xVariableIndex], out var xVariableName)
            || !TryNormalizeVariableName(tokens[yVariableIndex], out var yVariableName)
            || x2 <= x1
            || y2 <= y1)
        {
            return false;
        }

        var tolerance = 0;
        if (tokens.Length is 10 or 11)
        {
            if (!RunScriptScreenReadingStepParser.IsPixelSearchToleranceKeyword(tokens[toleranceKeywordIndex]))
            {
                return false;
            }

            if (!TryParseInteger(tokens[toleranceKeywordIndex + 1], out tolerance) || tolerance is < 0 or > byte.MaxValue)
            {
                return false;
            }
        }

        var foundName = hasFoundVariable && TryNormalizeVariableName(tokens[6], out var foundVariableName)
            ? foundVariableName
            : EditorActionScreenReadingPayload.DefaultFoundVariableName;
        action = new EditorAction();
        action.ApplyScreenReadingPayload(EditorActionScreenReadingPayload.ForPixelSearch(
            x1,
            y1,
            x2 - x1,
            y2 - y1,
            colorHex,
            foundName,
            xVariableName,
            yVariableName,
            tolerance));
        action.ScreenTargetColorSource = colorSource;
        action.ScreenTargetColorVariableName = targetColorVariableName;
        return true;
    }

    private static bool TryParseTargetColorToken(
        string token,
        out EditorActionScreenTargetColorSource colorSource,
        out string colorHex,
        out string variableName)
    {
        colorSource = EditorActionScreenTargetColorSource.ManualHex;
        colorHex = EditorActionScreenReadingPayload.DefaultColorHex;
        variableName = EditorActionScreenReadingPayload.DefaultTargetColorVariableName;

        if (ScreenPixelColor.TryParse(token, out var color))
        {
            colorHex = color.ToString();
            return true;
        }

        if (!token.StartsWith("$", StringComparison.Ordinal)
            || !TryNormalizeVariableName(token, out variableName))
        {
            return false;
        }

        colorSource = EditorActionScreenTargetColorSource.Variable;
        return true;
    }

    private static bool TryParseInteger(string value, out int result)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseIncDecStep(string step, string keyword, EditorActionType actionType, out EditorAction action)
    {
        action = new EditorAction();
        if (!step.StartsWith($"{keyword} ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[(keyword.Length + 1)..].Trim();
        if (payload.Length == 0)
        {
            return false;
        }

        var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        if (!TryNormalizeVariableName(parts[0], out var variableName))
        {
            action = new EditorAction
            {
                Type = actionType,
                Text = payload
            };
            return true;
        }

        var amountToken = parts.Length > 1 ? parts[1] : "1";
        if (!TryParseNumericToken(amountToken, out var sourceType, out var tokenValue))
        {
            action = new EditorAction
            {
                Type = actionType,
                Text = payload
            };
            return true;
        }

        action = new EditorAction
        {
            Type = actionType,
            ScriptVariableName = variableName,
            ScriptNumericSourceType = sourceType,
            ScriptNumericValue = tokenValue
        };
        return true;
    }

    private static bool TryParseRepeatStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!step.StartsWith("repeat ", StringComparison.OrdinalIgnoreCase)
            || !step.EndsWith("{", StringComparison.Ordinal))
        {
            return false;
        }

        var token = step[7..^1].Trim();
        if (token.Length == 0)
        {
            return false;
        }

        if (TryParseNumericToken(token, out var sourceType, out var tokenValue))
        {
            action = new EditorAction
            {
                Type = EditorActionType.RepeatBlockStart,
                ScriptNumericSourceType = sourceType,
                ScriptNumericValue = tokenValue
            };
            return true;
        }

        action = new EditorAction
        {
            Type = EditorActionType.RepeatBlockStart,
            Text = token
        };
        return true;
    }

    private static bool TryParseConditionStep(string step, string keyword, EditorActionType actionType, out EditorAction action)
    {
        action = new EditorAction();
        if (!step.StartsWith($"{keyword} ", StringComparison.OrdinalIgnoreCase)
            || !step.EndsWith("{", StringComparison.Ordinal))
        {
            return false;
        }

        var condition = step[(keyword.Length + 1)..^1].Trim();
        if (condition.Length == 0)
        {
            return false;
        }

        if (RunScriptConditionParser.TryParse(condition, out var parsedCondition, out _)
            && parsedCondition != null
            && TryParseOperandToken(parsedCondition.LeftToken, out var leftType, out var leftValue)
            && TryParseOperandToken(parsedCondition.RightToken, out var rightType, out var rightValue)
            && TryMapConditionOperatorToken(parsedCondition.OperatorToken, out var conditionOperator))
        {
            action = new EditorAction
            {
                Type = actionType,
                ScriptLeftOperandType = leftType,
                ScriptLeftOperand = leftValue,
                ScriptConditionOperator = conditionOperator,
                ScriptRightOperandType = rightType,
                ScriptRightOperand = rightValue
            };
            return true;
        }

        action = new EditorAction
        {
            Type = actionType,
            Text = condition
        };
        return true;
    }

    private static bool TryMapConditionOperatorToken(string operatorToken, out ScriptConditionOperator conditionOperator)
    {
        conditionOperator = operatorToken switch
        {
            "==" => ScriptConditionOperator.Equals,
            "!=" => ScriptConditionOperator.NotEquals,
            ">" => ScriptConditionOperator.GreaterThan,
            ">=" => ScriptConditionOperator.GreaterThanOrEqual,
            "<" => ScriptConditionOperator.LessThan,
            "<=" => ScriptConditionOperator.LessThanOrEqual,
            _ => ScriptConditionOperator.Equals
        };

        return operatorToken is "==" or "!=" or ">" or ">=" or "<" or "<=";
    }

    private static bool TryParseForStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!step.StartsWith("for ", StringComparison.OrdinalIgnoreCase)
            || !step.EndsWith("{", StringComparison.Ordinal))
        {
            return false;
        }

        var body = step[4..^1].Trim();
        if (body.Length == 0)
        {
            return false;
        }

        var tokens = body.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 5
            || !tokens[1].Equals("from", StringComparison.OrdinalIgnoreCase)
            || !tokens[3].Equals("to", StringComparison.OrdinalIgnoreCase))
        {
            action = new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                Text = body
            };
            return true;
        }

        if (tokens.Length != 5 && tokens.Length != 7)
        {
            action = new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                Text = body
            };
            return true;
        }

        if (!TryNormalizeVariableName(tokens[0], out var variableName)
            || !TryParseNumericToken(tokens[2], out var startType, out var startValue)
            || !TryParseNumericToken(tokens[4], out var endType, out var endValue))
        {
            action = new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                Text = body
            };
            return true;
        }

        var hasStep = false;
        var stepType = ScriptNumericSourceType.Number;
        var stepValue = "1";
        if (tokens.Length == 7)
        {
            if (!tokens[5].Equals("step", StringComparison.OrdinalIgnoreCase)
                || !TryParseNumericToken(tokens[6], out stepType, out stepValue))
            {
                action = new EditorAction
                {
                    Type = EditorActionType.ForBlockStart,
                    Text = body
                };
                return true;
            }

            hasStep = true;
        }

        action = new EditorAction
        {
            Type = EditorActionType.ForBlockStart,
            ForVariableName = variableName,
            ForStartType = startType,
            ForStartValue = startValue,
            ForEndType = endType,
            ForEndValue = endValue,
            ForHasStep = hasStep,
            ForStepType = stepType,
            ForStepValue = stepValue
        };
        return true;
    }

    private static bool TryParseNumericToken(string rawToken, out ScriptNumericSourceType sourceType, out string tokenValue)
    {
        sourceType = ScriptNumericSourceType.Number;
        tokenValue = string.Empty;

        var token = rawToken.Trim();
        if (token.Length == 0)
        {
            return false;
        }

        if (token.StartsWith("$", StringComparison.Ordinal))
        {
            var variable = token[1..].Trim();
            if (!TryNormalizeVariableName(variable, out tokenValue))
            {
                return false;
            }

            sourceType = ScriptNumericSourceType.VariableReference;
            return true;
        }

        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        sourceType = ScriptNumericSourceType.Number;
        tokenValue = number.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryParseOperandToken(string rawToken, out ScriptOperandType operandType, out string tokenValue)
    {
        operandType = ScriptOperandType.Text;
        tokenValue = string.Empty;

        var token = rawToken.Trim();
        if (token.Length == 0)
        {
            return false;
        }

        if (token.StartsWith("$$", StringComparison.Ordinal))
        {
            operandType = ScriptOperandType.Text;
        tokenValue = EditorActionScriptTokens.UnescapeLiteralDollar(token);
            return true;
        }

        if (token.StartsWith("$", StringComparison.Ordinal))
        {
            var variable = token[1..].Trim();
            if (!TryNormalizeVariableName(variable, out tokenValue))
            {
                return false;
            }

            operandType = ScriptOperandType.VariableReference;
            return true;
        }

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            operandType = ScriptOperandType.Number;
            tokenValue = number.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (bool.TryParse(token, out var boolValue))
        {
            operandType = ScriptOperandType.Boolean;
            tokenValue = boolValue.ToString().ToLowerInvariant();
            return true;
        }

        operandType = ScriptOperandType.Text;
            tokenValue = EditorActionScriptTokens.UnescapeLiteralDollar(token);
        return true;
    }

    private static bool TryInferSetValue(string rawValue, out ScriptValueType valueType, out string value)
    {
        valueType = ScriptValueType.Text;
        value = string.Empty;

        var token = rawValue.Trim();
        if (token.Length == 0)
        {
            return false;
        }

        if (token.StartsWith("$$", StringComparison.Ordinal))
        {
            valueType = ScriptValueType.Text;
            value = EditorActionScriptTokens.UnescapeLiteralDollar(token);
            return true;
        }

        if (token.StartsWith("$", StringComparison.Ordinal))
        {
            var variable = token[1..].Trim();
            if (!TryNormalizeVariableName(variable, out value))
            {
                return false;
            }

            valueType = ScriptValueType.VariableReference;
            return true;
        }

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            valueType = ScriptValueType.Number;
            value = number.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (bool.TryParse(token, out var boolValue))
        {
            valueType = ScriptValueType.Boolean;
            value = boolValue.ToString().ToLowerInvariant();
            return true;
        }

        valueType = ScriptValueType.Text;
        value = EditorActionScriptTokens.UnescapeLiteralDollar(token);
        return true;
    }

    private static bool TryNormalizeVariableName(string rawValue, out string variableName)
    {
        variableName = EditorActionScriptTokens.NormalizeVariableToken(rawValue);

        return EditorActionScriptTokens.IsValidVariableName(variableName);
    }
    
    private static void AppendDelayActions(
        ICollection<EditorAction> actions,
        int fixedDelayMs,
        bool hasRandomDelay,
        int randomDelayMinMs,
        int randomDelayMaxMs)
    {
        if (fixedDelayMs > 0)
        {
            actions.Add(new EditorAction
            {
                Type = EditorActionType.Delay,
                DelayMs = fixedDelayMs,
                UseRandomDelay = false
            });
        }

        if (hasRandomDelay)
        {
            actions.Add(new EditorAction
            {
                Type = EditorActionType.Delay,
                UseRandomDelay = true,
                RandomDelayMinMs = randomDelayMinMs,
                RandomDelayMaxMs = randomDelayMaxMs
            });
        }
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
