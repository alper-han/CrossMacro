using System;
using System.Collections.Generic;
using System.Globalization;
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
                    DelayMs = action.DelayMs
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
                    UseCurrentPosition = action.UseCurrentPosition
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
                    UseCurrentPosition = action.UseCurrentPosition
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
                    UseCurrentPosition = action.UseCurrentPosition
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
                action.UseCurrentPosition = ev.UseCurrentPosition;
                break;
                
            case EventType.ButtonRelease:
                action.Type = EditorActionType.MouseUp;
                action.X = ev.X;
                action.Y = ev.Y;
                action.Button = ev.Button;
                action.UseCurrentPosition = ev.UseCurrentPosition;
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
                yield return $"type {action.Text}";
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

        var name = NormalizeVariableToken(action.ScriptVariableName);
        var value = action.ScriptValueType switch
        {
            ScriptValueType.VariableReference => $"${NormalizeVariableToken(action.ScriptValue)}",
            ScriptValueType.Boolean => bool.TryParse(action.ScriptValue, out var boolValue)
                ? boolValue.ToString().ToLowerInvariant()
                : action.ScriptValue.Trim(),
            ScriptValueType.Text => EscapeLiteralDollar(action.ScriptValue.Trim()),
            _ => action.ScriptValue.Trim()
        };

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

        var variableName = NormalizeVariableToken(action.ScriptVariableName);
        var amountToken = BuildNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue);
        return $"inc {variableName} {amountToken}";
    }

    private static string BuildDecrementStep(EditorAction action)
    {
        if (ShouldSerializeLegacyNumericUpdateText(action))
        {
            return $"dec {action.Text}";
        }

        var variableName = NormalizeVariableToken(action.ScriptVariableName);
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
        var op = action.ScriptConditionOperator switch
        {
            ScriptConditionOperator.Equals => "==",
            ScriptConditionOperator.NotEquals => "!=",
            ScriptConditionOperator.GreaterThan => ">",
            ScriptConditionOperator.GreaterThanOrEqual => ">=",
            ScriptConditionOperator.LessThan => "<",
            ScriptConditionOperator.LessThanOrEqual => "<=",
            _ => "=="
        };
        var right = BuildOperandToken(action.ScriptRightOperandType, action.ScriptRightOperand);
        return $"{keyword} {left} {op} {right} {{";
    }

    private static string BuildForStep(EditorAction action)
    {
        if (ShouldSerializeLegacyForText(action))
        {
            return $"for {action.Text} {{";
        }

        var variableName = NormalizeVariableToken(action.ForVariableName);
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
        var token = value.Trim();
        return sourceType == ScriptNumericSourceType.VariableReference
            ? $"${NormalizeVariableToken(token)}"
            : token;
    }

    private static string BuildOperandToken(ScriptOperandType operandType, string value)
    {
        var token = value.Trim();
        return operandType switch
        {
            ScriptOperandType.VariableReference => $"${NormalizeVariableToken(token)}",
            ScriptOperandType.Text => EscapeLiteralDollar(token),
            _ => token
        };
    }

    private static string NormalizeVariableToken(string value)
    {
        var token = value.Trim();
        return token.StartsWith('$') ? token[1..] : token;
    }

    private static string EscapeLiteralDollar(string value)
    {
        return value.Replace("$", "$$", StringComparison.Ordinal);
    }

    private static string UnescapeLiteralDollar(string value)
    {
        return value.Replace("$$", "$", StringComparison.Ordinal);
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
                    AppendDelayActions(
                        actions,
                        ev.DelayMs,
                        ev.HasRandomDelay,
                        ev.RandomDelayMinMs,
                        ev.RandomDelayMaxMs);
                    textAction.DelayMs = 0;
                    textAction.UseRandomDelay = false;
                    textAction.RandomDelayMinMs = 0;
                    textAction.RandomDelayMaxMs = 0;
                    actions.Add(textAction);
                    i += consumed - 1; // -1 because loop will increment
                    continue;
                }
            }
            
            var action = FromMacroEvent(ev, nextEvent);
            
            // Set IsAbsolute based on sequence metadata for coordinate-bearing mouse actions.
            if (action.Type is EditorActionType.MouseMove
                or EditorActionType.MouseClick
                or EditorActionType.MouseDown
                or EditorActionType.MouseUp)
            {
                action.IsAbsolute = sequence.IsAbsoluteCoordinates;

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
                if (isAbsoluteMove)
                {
                    hasAbsoluteCursorPosition = true;
                    absoluteCursorX = moveX;
                    absoluteCursorY = moveY;
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

                if (hasAbsoluteCursorPosition)
                {
                    actions.Add(CreatePositionedButtonAction(
                        currentButtonKeyword,
                        currentButton,
                        isAbsolute: true,
                        absoluteCursorX,
                        absoluteCursorY));
                    continue;
                }

                actions.Add(CreateCurrentPositionButtonAction(currentButtonKeyword, currentButton));
                continue;
            }

            if (TryParseTapStep(step, out var tapKeyCode))
            {
                actions.Add(new EditorAction
                {
                    Type = EditorActionType.KeyPress,
                    KeyCode = tapKeyCode
                });
                continue;
            }

            if (TryParseKeyStep(step, out var keyActionType, out var keyCode))
            {
                actions.Add(new EditorAction
                {
                    Type = keyActionType,
                    KeyCode = keyCode
                });
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
                    Text = text
                });
                continue;
            }

            if (TryParseSetStep(step, out var setAction))
            {
                actions.Add(setAction);
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
            tokenValue = UnescapeLiteralDollar(token);
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
        tokenValue = UnescapeLiteralDollar(token);
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
            value = UnescapeLiteralDollar(token);
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
        value = UnescapeLiteralDollar(token);
        return true;
    }

    private static bool TryNormalizeVariableName(string rawValue, out string variableName)
    {
        variableName = rawValue.Trim();
        if (variableName.StartsWith("$", StringComparison.Ordinal))
        {
            variableName = variableName[1..].Trim();
        }

        return IsValidVariableName(variableName);
    }

    private static bool IsValidVariableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!(value[0] == '_' || char.IsLetter(value[0])))
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!(value[i] == '_' || char.IsLetterOrDigit(value[i])))
            {
                return false;
            }
        }

        return true;
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

            text.Append(c.Value);
            consumed++;
        }
        
        if (text.Length < 2)
            return (null, 0);
        
        return (new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = text.ToString()
        }, consumed);
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
