
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

    /// <summary>
    /// Converts the editor projection while retaining the existing conversion
    /// implementation as the compatibility facade.
    /// </summary>
    public MacroSequence ToMacroSequence(EditorMacroProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return ToMacroSequence(
            projection.Actions,
            projection.Name,
            projection.IsAbsoluteCoordinates,
            projection.SkipInitialZeroZero);
    }

    /// <summary>
    /// Restores a runtime sequence into the editor projection boundary.
    /// </summary>
    public EditorMacroProjection FromMacroSequenceProjection(MacroSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        return new EditorMacroProjection(
            FromMacroSequence(sequence),
            sequence.Name,
            sequence.IsAbsoluteCoordinates,
            sequence.SkipInitialZeroZero);
    }

    /// <inheritdoc/>
    public IReadOnlyList<MacroEvent> ToMacroEvents(EditorAction action)
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
                    CoordinateMode = action.IsAbsolute ? MouseCoordinateMode.Absolute : MouseCoordinateMode.Relative,
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
                    CoordinateMode = GetCoordinateMode(action),
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
                    CoordinateMode = GetCoordinateMode(action),
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
                    CoordinateMode = GetCoordinateMode(action),
                });
                break;

            case EditorActionType.KeyPress:
                // KeyPress expands to KeyDown + KeyUp
                events.Add(new MacroEvent
                {
                    Type = EventType.KeyPress,
                    KeyCode = action.KeyCode,
                    DelayMs = action.DelayMs,
                });
                events.Add(new MacroEvent
                {
                    Type = EventType.KeyRelease,
                    KeyCode = action.KeyCode,
                    DelayMs = DefaultKeyPressDelayMs,
                });
                break;

            case EditorActionType.KeyDown:
                events.Add(new MacroEvent
                {
                    Type = EventType.KeyPress,
                    KeyCode = action.KeyCode,
                    DelayMs = action.DelayMs,
                });
                break;

            case EditorActionType.KeyUp:
                events.Add(new MacroEvent
                {
                    Type = EventType.KeyRelease,
                    KeyCode = action.KeyCode,
                    DelayMs = action.DelayMs,
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
                    RandomDelayMaxMs = action.UseRandomDelay ? action.RandomDelayMaxMs : 0,
                });
                break;

            case EditorActionType.ScrollVertical:
                var scrollButton = action.ScrollAmount > 0 ? MacroMouseButton.ScrollUp : MacroMouseButton.ScrollDown;
                for (int i = 0; i < Math.Abs(action.ScrollAmount); i++)
                {
                    events.Add(new MacroEvent
                    {
                        Type = EventType.Click,
                        Button = scrollButton,
                        DelayMs = i is 0 ? action.DelayMs : 0,
                    });
                }
                break;

            case EditorActionType.ScrollHorizontal:
                var hScrollButton = action.ScrollAmount > 0 ? MacroMouseButton.ScrollRight : MacroMouseButton.ScrollLeft;
                for (int i = 0; i < Math.Abs(action.ScrollAmount); i++)
                {
                    events.Add(new MacroEvent
                    {
                        Type = EventType.Click,
                        Button = hScrollButton,
                        DelayMs = i is 0 ? action.DelayMs : 0,
                    });
                }
                break;

            case EditorActionType.TextInput:
                var preservedTextInputEvents = action.GetPreservedTextInputEvents();
                if (preservedTextInputEvents is not null)
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
                    if (keyCode == -1)
                    {
                        continue; // Skip unmappable characters
                    }

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
            case EditorActionType.ImageSearch:
            case EditorActionType.ImageClick:
            case EditorActionType.WaitImage:
            case EditorActionType.ClipboardGet:
            case EditorActionType.ClipboardSet:
            case EditorActionType.ShellCommand:
            case EditorActionType.Screenshot:
            case EditorActionType.WindowCommand:
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
            _ => -1,
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
                DelayMs = 0,
            });
        }

        if (needsAltGr)
        {
            events.Add(new MacroEvent
            {
                Type = EventType.KeyPress,
                KeyCode = InputEventCode.KEY_RIGHTALT,
                DelayMs = 0,
            });
        }

        events.Add(new MacroEvent
        {
            Type = EventType.KeyPress,
            KeyCode = keyCode,
            DelayMs = isFirst ? initialDelayMs : DefaultKeyPressDelayMs,
        });
        events.Add(new MacroEvent
        {
            Type = EventType.KeyRelease,
            KeyCode = keyCode,
            DelayMs = 0,
        });

        if (needsAltGr)
        {
            events.Add(new MacroEvent
            {
                Type = EventType.KeyRelease,
                KeyCode = InputEventCode.KEY_RIGHTALT,
                DelayMs = 0,
            });
        }

        if (needsShift)
        {
            events.Add(new MacroEvent
            {
                Type = EventType.KeyRelease,
                KeyCode = InputEventCode.KEY_LEFTSHIFT,
                DelayMs = 0,
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
            KeyName = _keyCodeMapper.GetKeyName(keyCode),
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
            RandomDelayMaxMs = ev.RandomDelayMaxMs,
        };

        switch (ev.Type)
        {
            case EventType.MouseMove:
                action.Type = EditorActionType.MouseMove;
                action.X = ev.X;
                action.Y = ev.Y;
                if (ev.CoordinateMode is not null)
                {
                    action.IsAbsolute = ev.CoordinateMode.Value is MouseCoordinateMode.Absolute;
                }
                break;

            case EventType.Click:
                if (IsScrollButton(ev.Button))
                {
                    action.Type = ev.Button is MacroMouseButton.ScrollUp or MacroMouseButton.ScrollDown
                        ? EditorActionType.ScrollVertical
                        : EditorActionType.ScrollHorizontal;
                    action.ScrollAmount = ev.Button is MacroMouseButton.ScrollUp or MacroMouseButton.ScrollRight ? 1 : -1;
                }
                else
                {
                    action.Type = EditorActionType.MouseClick;
                    action.X = ev.X;
                    action.Y = ev.Y;
                    action.Button = ev.Button;
                    action.UseCurrentPosition = ev.UseCurrentPosition;
                    if (ev.CoordinateMode is not null)
                    {
                        action.IsAbsolute = ev.CoordinateMode.Value is MouseCoordinateMode.Absolute;
                    }
                }
                break;

            case EventType.ButtonPress:
                action.Type = EditorActionType.MouseDown;
                action.X = ev.X;
                action.Y = ev.Y;
                action.Button = ev.Button;
                action.UseCurrentPosition = ev.UseCurrentPosition;
                if (ev.CoordinateMode is not null)
                {
                    action.IsAbsolute = ev.CoordinateMode.Value is MouseCoordinateMode.Absolute;
                }
                break;

            case EventType.ButtonRelease:
                action.Type = EditorActionType.MouseUp;
                action.X = ev.X;
                action.Y = ev.Y;
                action.Button = ev.Button;
                action.UseCurrentPosition = ev.UseCurrentPosition;
                if (ev.CoordinateMode is not null)
                {
                    action.IsAbsolute = ev.CoordinateMode.Value is MouseCoordinateMode.Absolute;
                }
                break;

            case EventType.KeyPress:
                // Check if next event is KeyRelease with same key - then merge to KeyPress
                if ((nextEvent?.Type) is EventType.KeyRelease && nextEvent?.KeyCode == ev.KeyCode)
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
            CreatedAt = DateTime.UtcNow,
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
                if (ev.Type is EventType.None)
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

            if (action.Type is EditorActionType.TextInput && actionEventCount > 0)
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
        sequence.MouseMoveCount = sequence.Events.Count(e => e.Type is EventType.MouseMove);
        sequence.ClickCount = sequence.Events.Count(e => e.Type is not EventType.MouseMove);

        if (hasStateScriptActions)
        {
            sequence.SkipInitialZeroZero = true;
            sequence.ReplaceScriptSteps(BuildScriptSteps(actionList)
                .Select(step => step.Step)
                .Where(step => !string.IsNullOrWhiteSpace(step))
                .ToList());
        }

        return sequence;
    }

    private MacroSequence CompileScriptBackedSequence(IReadOnlyList<EditorAction> actions, string name)
    {
        var scriptSteps = BuildScriptSteps(actions);
        var compileResult = _runScriptCompiler.Compile(scriptSteps);
        if (!compileResult.Success || compileResult.Sequence is null)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(compileResult.ErrorMessage)
                ? "Script compilation failed."
                : compileResult.ErrorMessage);
        }

        var sequence = compileResult.Sequence;
        sequence.Name = name;
        sequence.CreatedAt = DateTime.UtcNow;
        sequence.ReplaceScriptSteps(scriptSteps
            .Select(step => step.Step)
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .ToList());

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
        sequence.MouseMoveCount = sequence.Events.Count(e => e.Type is EventType.MouseMove);
        sequence.ClickCount = sequence.Events.Count(e => e.Type is not EventType.MouseMove);
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

    private static List<RunScriptStep> BuildScriptSteps(IReadOnlyList<EditorAction> actions)
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
                steps.Add(new RunScriptStep(step, SourceLineNumber: null, sourceIndex));
            }
        }

        return steps;
    }

    private static IEnumerable<string> ConvertActionToScriptSteps(EditorAction action)
    {
        switch (action.Type)
        {
            case EditorActionType.MouseMove:
                yield return $"move {(action.IsAbsolute ? "abs" : "rel")} {action.X.ToString(CultureInfo.InvariantCulture)} {action.Y.ToString(CultureInfo.InvariantCulture)}";
                yield break;

            case EditorActionType.MouseClick:
                if (action.UseCurrentPosition)
                {
                    yield return $"click {RunScriptSyntax.CurrentPositionToken} {ToButtonToken(action.Button)}";
                }
                else
                {
                    yield return $"move {(action.IsAbsolute ? "abs" : "rel")} {action.X.ToString(CultureInfo.InvariantCulture)} {action.Y.ToString(CultureInfo.InvariantCulture)}";
                    yield return $"click {ToButtonToken(action.Button)}";
                }

                yield break;

            case EditorActionType.MouseDown:
                if (!action.UseCurrentPosition)
                {
                    yield return $"move {(action.IsAbsolute ? "abs" : "rel")} {action.X.ToString(CultureInfo.InvariantCulture)} {action.Y.ToString(CultureInfo.InvariantCulture)}";
                }

                yield return action.UseCurrentPosition
                    ? $"down {RunScriptSyntax.CurrentPositionToken} {ToButtonToken(action.Button)}"
                    : $"down {ToButtonToken(action.Button)}";
                yield break;

            case EditorActionType.MouseUp:
                if (!action.UseCurrentPosition)
                {
                    yield return $"move {(action.IsAbsolute ? "abs" : "rel")} {action.X.ToString(CultureInfo.InvariantCulture)} {action.Y.ToString(CultureInfo.InvariantCulture)}";
                }

                yield return action.UseCurrentPosition
                    ? $"up {RunScriptSyntax.CurrentPositionToken} {ToButtonToken(action.Button)}"
                    : $"up {ToButtonToken(action.Button)}";
                yield break;

            case EditorActionType.KeyPress:
                yield return $"tap {action.KeyCode.ToString(CultureInfo.InvariantCulture)}";
                yield break;

            case EditorActionType.KeyDown:
                yield return $"key down {action.KeyCode.ToString(CultureInfo.InvariantCulture)}";
                yield break;

            case EditorActionType.KeyUp:
                yield return $"key up {action.KeyCode.ToString(CultureInfo.InvariantCulture)}";
                yield break;

            case EditorActionType.Delay:
                yield return action.UseRandomDelay
                    ? $"delay random {action.RandomDelayMinMs.ToString(CultureInfo.InvariantCulture)} {action.RandomDelayMaxMs.ToString(CultureInfo.InvariantCulture)}"
                    : $"delay {action.DelayMs.ToString(CultureInfo.InvariantCulture)}";
                yield break;

            case EditorActionType.ScrollVertical:
                yield return action.ScrollAmount > 0
                    ? $"scroll up {Math.Abs(action.ScrollAmount).ToString(CultureInfo.InvariantCulture)}"
                    : $"scroll down {Math.Abs(action.ScrollAmount).ToString(CultureInfo.InvariantCulture)}";
                yield break;

            case EditorActionType.ScrollHorizontal:
                yield return action.ScrollAmount > 0
                    ? $"scroll right {Math.Abs(action.ScrollAmount).ToString(CultureInfo.InvariantCulture)}"
                    : $"scroll left {Math.Abs(action.ScrollAmount).ToString(CultureInfo.InvariantCulture)}";
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

            case EditorActionType.PixelColor:
                yield return BuildPixelColorStep(action);
                yield break;

            case EditorActionType.WaitColor:
                yield return BuildWaitColorStep(action);
                yield break;

            case EditorActionType.PixelSearch:
                yield return BuildPixelSearchStep(action);
                yield break;

            case EditorActionType.ImageSearch:
                yield return BuildImageSearchStep(action);
                yield break;

            case EditorActionType.ImageClick:
                yield return BuildImageClickStep(action);
                yield break;

            case EditorActionType.WaitImage:
                yield return BuildWaitImageStep(action);
                yield break;

            case EditorActionType.ClipboardGet:
                yield return $"clipboard get {EditorActionScriptTokens.NormalizeVariableToken(action.ScriptVariableName)}";
                yield break;

            case EditorActionType.ClipboardSet:
                yield return $"clipboard set {action.Text}";
                yield break;

            case EditorActionType.ShellCommand:
                yield return BuildShellStep(action);
                yield break;

            case EditorActionType.Screenshot:
                yield return BuildScreenshotStep(action);
                yield break;

            case EditorActionType.WindowCommand:
                yield return BuildWindowStep(action);
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
            ? $"pixelcolor {payload.ScreenX.ToString(CultureInfo.InvariantCulture)} {payload.ScreenY.ToString(CultureInfo.InvariantCulture)} {variableName} timeout {payload.ScreenTimeoutMs.ToString(CultureInfo.InvariantCulture)}"
            : $"pixelcolor rel {payload.ScreenX.ToString(CultureInfo.InvariantCulture)} {payload.ScreenY.ToString(CultureInfo.InvariantCulture)} {variableName} timeout {payload.ScreenTimeoutMs.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string BuildWaitColorStep(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);
        var resultVariableName = payload.NormalizeColorVariableToken();
        return $"waitcolor {payload.ScreenX.ToString(CultureInfo.InvariantCulture)} {payload.ScreenY.ToString(CultureInfo.InvariantCulture)} {payload.FormatTargetColorToken()} {payload.ScreenTimeoutMs.ToString(CultureInfo.InvariantCulture)} {resultVariableName}";
    }

    private static string BuildPixelSearchStep(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);
        var foundVariableName = payload.NormalizeFoundVariableToken();
        var xVariableName = payload.NormalizeFoundXVariableToken();
        var yVariableName = payload.NormalizeFoundYVariableToken();
        return $"pixelsearch {payload.ScreenLeft.ToString(CultureInfo.InvariantCulture)} {payload.ScreenTop.ToString(CultureInfo.InvariantCulture)} {payload.ScreenRight.ToString(CultureInfo.InvariantCulture)} {payload.ScreenBottom.ToString(CultureInfo.InvariantCulture)} {payload.FormatTargetColorToken()} {foundVariableName} {xVariableName} {yVariableName} timeout {payload.ScreenTimeoutMs.ToString(CultureInfo.InvariantCulture)} tolerance {payload.ScreenTolerance.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string BuildImageSearchStep(EditorAction action)
    {
        return BuildImageActionPrefix(RunScriptSyntax.ImageSearchCommand, action)
            + $" {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundVariableName)} {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundXVariableName)} {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundYVariableName)}"
            + BuildImageActionMatchOptions(action);
    }

    private static string BuildImageClickStep(EditorAction action)
    {
        return BuildImageActionPrefix(RunScriptSyntax.ImageClickCommand, action)
            + $" {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundVariableName)} {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundXVariableName)} {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundYVariableName)}"
            + $" button {ToImageClickButtonToken(action.Button)}"
            + BuildImageActionMatchOptions(action);
    }

    private static string BuildWaitImageStep(EditorAction action)
    {
        return BuildImageActionPrefix(RunScriptSyntax.WaitImageCommand, action)
            + $" {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundVariableName)} {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundXVariableName)} {EditorActionScriptTokens.NormalizeVariableToken(action.ScreenFoundYVariableName)}"
            + BuildImageActionMatchOptions(action);
    }

    private static string BuildImageActionPrefix(string command, EditorAction action)
    {
        var imageName = EditorActionScriptTokens.NormalizeVariableToken(action.ImageAssetName);
        var right = checked(action.ScreenLeft + action.ScreenWidth);
        var bottom = checked(action.ScreenTop + action.ScreenHeight);
        return $"{command} {action.ScreenLeft.ToString(CultureInfo.InvariantCulture)} {action.ScreenTop.ToString(CultureInfo.InvariantCulture)} {right.ToString(CultureInfo.InvariantCulture)} {bottom.ToString(CultureInfo.InvariantCulture)} {imageName}";
    }

    private static string BuildImageActionMatchOptions(EditorAction action)
    {
        var similarity = action.ImageSearchSimilarity.ToString("0.################", CultureInfo.InvariantCulture);
        var mode = action.ImageSearchMatchModeWasExplicit || action.ImageSearchMatchMode is not EditorImageMatchMode.FirstThresholdMatch
            ? $" matchmode {RunScriptPlatformSyntax.ToImageMatchModeToken(action.ImageSearchMatchMode)}"
            : string.Empty;
        var scaleAware = action.ImageSearchScaleAware ? $" {RunScriptSyntax.ImageSearchScaleAwareKeyword}" : string.Empty;
        return $" timeout {action.ScreenTimeoutMs.ToString(CultureInfo.InvariantCulture)} similarity {similarity} downsample {action.ImageSearchDownsample.ToString(CultureInfo.InvariantCulture)}{mode}{scaleAware}";
    }

    private static string BuildShellStep(EditorAction action)
    {
        if (!action.TryGetShellPayload(out var payload))
        {
            throw new ArgumentException("Action type must be a shell command.", nameof(action));
        }

        var command = QuoteShellField(payload.Command);
        var options = BuildShellOptions(payload);
        return payload.Mode switch
        {
            ShellCommandMode.ShellCapture => $"shell capture {command} {FormatShellCaptureTarget(payload.ExitCodeVariableName)} {FormatShellCaptureTarget(payload.StandardOutputVariableName)} {FormatShellCaptureTarget(payload.StandardErrorVariableName)}{options}",
            ShellCommandMode.ShellInput => $"shell input {QuoteShellField(payload.StandardInput)} {command}{options}",
            ShellCommandMode.ShellCaptureInput => $"shell capture-input {QuoteShellField(payload.StandardInput)} {command} {FormatShellCaptureTarget(payload.ExitCodeVariableName)} {FormatShellCaptureTarget(payload.StandardOutputVariableName)} {FormatShellCaptureTarget(payload.StandardErrorVariableName)}{options}",
            _ => $"shell {command}{options}",
        };
    }

    private static string BuildScreenshotStep(EditorAction action)
    {
        if (!action.TryGetScreenshotPayload(out var payload))
        {
            throw new ArgumentException("Action type must be a screenshot.", nameof(action));
        }

        var parts = new List<string> { RunScriptSyntax.ScreenshotCommand };
        if (payload.UseRegion)
        {
            parts.AddRange(["region", payload.RegionX, payload.RegionY, payload.RegionWidth, payload.RegionHeight]);
        }

        if (!string.IsNullOrWhiteSpace(payload.OutputPath))
        {
            parts.Add("output");
            parts.Add(QuoteScreenshotOutputPath(payload.OutputPath));
        }

        if (payload.CopyToClipboard)
        {
            parts.Add("clipboard");
        }

        return string.Join(' ', parts);
    }

    internal static string BuildWindowStep(EditorAction action)
    {
        if (!action.TryGetWindowPayload(out var payload))
        {
            throw new ArgumentException("Action type must be a window command.", nameof(action));
        }

        var selectorKind = string.IsNullOrWhiteSpace(payload.SelectorKind) ? "title" : payload.SelectorKind.Trim().ToLowerInvariant();
        var selectorValue = QuoteWindowField(payload.SelectorValue);
        var outputVariable = EditorActionScriptTokens.NormalizeVariableToken(payload.OutputVariable);
        var workspace = QuoteWindowField(payload.Workspace);

        return payload.Mode switch
        {
            WindowCommandMode.Active => $"window active {payload.ActiveField} {outputVariable}",
            WindowCommandMode.Search => $"window search {selectorKind} {selectorValue} {outputVariable}",
            WindowCommandMode.Wait => $"window wait {selectorKind} {selectorValue} {payload.TimeoutMs.ToString(CultureInfo.InvariantCulture)} {outputVariable}",
            WindowCommandMode.Focus when selectorKind is "active" => "window focus active",
            WindowCommandMode.Focus => $"window focus {selectorKind} {selectorValue}",
            WindowCommandMode.Close when selectorKind is "active" => "window close active",
            WindowCommandMode.Close => $"window close {selectorKind} {selectorValue}",
            WindowCommandMode.Move => $"window move {payload.X.ToString(CultureInfo.InvariantCulture)} {payload.Y.ToString(CultureInfo.InvariantCulture)}",
            WindowCommandMode.Resize => $"window resize {payload.Width.ToString(CultureInfo.InvariantCulture)} {payload.Height.ToString(CultureInfo.InvariantCulture)}",
            WindowCommandMode.Center => "window center active",
            WindowCommandMode.Maximize => "window maximize active",
            WindowCommandMode.Fullscreen => "window fullscreen active",
            WindowCommandMode.Floating => "window float active",
            WindowCommandMode.WorkspaceGet => $"window getdesktop {outputVariable}",
            WindowCommandMode.WorkspaceSwitch => $"window setdesktop {workspace}",
            WindowCommandMode.WorkspaceMoveActive => $"window setdesktopforwindow active {workspace}",
            WindowCommandMode.WorkspaceMoveWindow => $"window setdesktopforwindow address {payload.SelectorValue.Trim()} {workspace}",
            _ => "window active title $windowResult",
        };
    }

    private static string QuoteWindowField(string value)
    {
        return $"\"{(value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static string QuoteScreenshotOutputPath(string value)
    {
        return value.Any(char.IsWhiteSpace) || value.Contains('"', StringComparison.Ordinal) || value.Contains('\\', StringComparison.Ordinal)
            ? $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;
    }

    private static string BuildShellOptions(EditorActionShellPayload payload)
    {
        if (payload.TimeoutMs > 0)
        {
            return $" {payload.Retries.ToString(CultureInfo.InvariantCulture)} {payload.BackoffMs.ToString(CultureInfo.InvariantCulture)} {payload.TimeoutMs.ToString(CultureInfo.InvariantCulture)}";
        }

        if (payload.BackoffMs > 0)
        {
            return $" {payload.Retries.ToString(CultureInfo.InvariantCulture)} {payload.BackoffMs.ToString(CultureInfo.InvariantCulture)}";
        }

        return payload.Retries > 0 ? $" {payload.Retries.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
    }

    private static string FormatShellCaptureTarget(string target)
    {
        return target is "_" ? "_" : EditorActionScriptTokens.NormalizeVariableToken(target);
    }

    private static string QuoteShellField(string value)
    {
        return $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static EditorActionScreenReadingPayload GetScreenReadingPayload(EditorAction action)
    {
        if (!action.TryGetScreenReadingPayload(out var payload))
        {
            throw new InvalidOperationException("Action type does not contain a screen-reading payload.");
        }

        return payload;
    }

    private static string ToButtonToken(MacroMouseButton button)
    {
        return button switch
        {
            MacroMouseButton.Left => "left",
            MacroMouseButton.Right => "right",
            MacroMouseButton.Middle => "middle",
            MacroMouseButton.Side1 => "side1",
            MacroMouseButton.Side2 => "side2",
            _ => "left",
        };
    }

    private static string ToImageClickButtonToken(MacroMouseButton button)
    {
        return button switch
        {
            MacroMouseButton.Right => "right",
            MacroMouseButton.Middle => "middle",
            _ => "left",
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

        if (action.ScriptValueType is ScriptValueType.Text
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
|| existingSteps.Count is 0
|| actionSteps.Count is 0)
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
    public IReadOnlyList<EditorAction> FromMacroSequence(MacroSequence sequence)
    {
        return FromMacroSequenceWithDiagnostics(sequence).Actions;
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
                    Text = textInputBoundary.Text,
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
            if (ev.Type is EventType.KeyRelease && i > 0)
            {
                var prevAction = actions.LastOrDefault();
                if ((prevAction?.Type) is EditorActionType.KeyPress && prevAction.KeyCode == ev.KeyCode)
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
 is MouseCoordinateMode.Absolute;
                }
            }

            if (action.Type is EditorActionType.Delay)
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
        if (sequence.TextInputBoundaries.Count is 0 || sequence.Events.Count is 0)
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

    private bool BoundaryMatchesTextInputEvents(IList<MacroEvent> events, TextInputBoundary boundary)
    {
        var expectedEvents = ToMacroEvents(new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = boundary.Text,
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
        IList<MacroEvent> events,
        int startEventIndex,
        int eventCount)
    {
        var preserved = new List<MacroEvent>(eventCount);
        for (var offset = 0; offset < eventCount; offset++)
        {
            var ev = events[startEventIndex + offset];
            if (offset is 0)
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
        IList<string>? scriptSteps,
        out List<EditorAction> actions,
        out List<EditorActionRestoreWarning> warnings)
    {
        actions = new List<EditorAction>();
        warnings = new List<EditorActionRestoreWarning>();
        if (scriptSteps is null || scriptSteps.Count is 0)
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
                    Y = moveY,
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

                if (currentMoveMode is MouseCoordinateMode.Absolute && hasAbsoluteCursorPosition)
                {
                    actions.Add(CreatePositionedButtonAction(
                        currentButtonKeyword,
                        currentButton,
                        isAbsolute: true,
                        absoluteCursorX,
                        absoluteCursorY));
                    continue;
                }

                if (currentMoveMode is MouseCoordinateMode.Relative)
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
                    RandomDelayMaxMs = useRandomDelay ? randomMax : 0,
                });
                continue;
            }

            if (TryParseScrollStep(step, out var scrollActionType, out var scrollAmount))
            {
                actions.Add(new EditorAction
                {
                    Type = scrollActionType,
                    ScrollAmount = scrollAmount,
                });
                continue;
            }

            if (TryParseTypeStep(stepForType, out var text))
            {
                actions.Add(new EditorAction
                {
                    Type = EditorActionType.TextInput,
                    Text = text,
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

            if (TryParseClipboardStep(stepForType, out var clipboardAction))
            {
                actions.Add(clipboardAction);
                continue;
            }

            if (TryParseShellStep(stepForType, out var shellAction))
            {
                actions.Add(shellAction);
                continue;
            }

            if (RunScriptPlatformSyntax.IsScreenshotStep(stepForType))
            {
                if (TryParseScreenshotStep(stepForType, out var screenshotAction))
                {
                    actions.Add(screenshotAction);
                }
                else
                {
                    warnings.Add(new EditorActionRestoreWarning(
                        index + 1,
                        step,
                        "Malformed screenshot step restored as raw script text."));
                    actions.Add(CreateRawScriptStepAction(step));
                }
                continue;
            }

            if (RunScriptSyntax.IsWindowStep(stepForType))
            {
                if (TryParseWindowStep(stepForType, out var windowAction))
                {
                    actions.Add(windowAction);
                }
                else
                {
                    warnings.Add(new EditorActionRestoreWarning(
                        index + 1,
                        step,
                        "Malformed window step restored as raw script text."));
                    actions.Add(CreateRawScriptStepAction(step));
                }

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
        if (tokens.Length is not 4 || !tokens[0].Equals("move", StringComparison.OrdinalIgnoreCase))
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
        out MacroMouseButton button,
        out bool isCurrentPositionExplicit)
    {
        keyword = string.Empty;
        button = MacroMouseButton.Left;
        isCurrentPositionExplicit = false;
        if (string.IsNullOrWhiteSpace(rawStep))
        {
            return false;
        }

        var step = rawStep.Trim();
        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length is < 2 or > 3)
        {
            return false;
        }

        if (!tokens[0].Equals("click", StringComparison.OrdinalIgnoreCase)
            && !tokens[0].Equals("down", StringComparison.OrdinalIgnoreCase)
            && !tokens[0].Equals("up", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (tokens.Length is 3)
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

    private static bool TryParseButtonToken(string token, out MacroMouseButton button)
    {
        button = token.ToLowerInvariant() switch
        {
            "left" or "l" => MacroMouseButton.Left,
            "right" or "r" => MacroMouseButton.Right,
            "middle" or "m" => MacroMouseButton.Middle,
            "side1" or "side" or "back" => MacroMouseButton.Side1,
            "side2" or "extra" or "forward" => MacroMouseButton.Side2,
            _ => MacroMouseButton.None,
        };

        return button is not MacroMouseButton.None;
    }

    private static EditorAction CreatePositionedButtonAction(string keyword, MacroMouseButton button, bool isAbsolute, int x, int y)
    {
        var actionType = keyword switch
        {
            "click" => EditorActionType.MouseClick,
            "down" => EditorActionType.MouseDown,
            "up" => EditorActionType.MouseUp,
            _ => EditorActionType.MouseClick,
        };

        return new EditorAction
        {
            Type = actionType,
            Button = button,
            IsAbsolute = isAbsolute,
            X = x,
            Y = y,
            UseCurrentPosition = false,
        };
    }

    private static EditorAction CreateCurrentPositionButtonAction(string keyword, MacroMouseButton button)
    {
        var actionType = keyword switch
        {
            "click" => EditorActionType.MouseClick,
            "down" => EditorActionType.MouseDown,
            "up" => EditorActionType.MouseUp,
            _ => EditorActionType.MouseClick,
        };

        return new EditorAction
        {
            Type = actionType,
            Button = button,
            IsAbsolute = false,
            X = 0,
            Y = 0,
            UseCurrentPosition = true,
        };
    }

    private static EditorAction CreateRawScriptStepAction(string step)
    {
        return new EditorAction
        {
            Type = EditorActionType.RawScriptStep,
            Text = step,
        };
    }

    private bool TryParseTapStep(string step, out int keyCode)
    {
        keyCode = 0;
        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length is not 2 || !tokens[0].Equals("tap", StringComparison.OrdinalIgnoreCase))
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
        if (tokens.Length is not 3 || !tokens[0].Equals("key", StringComparison.OrdinalIgnoreCase))
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

        if (tokens.Length is 2)
        {
            return int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out fixedDelayMs);
        }

        if (tokens.Length is 4 && tokens[1].Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out randomMinDelayMs)
                || !int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out randomMaxDelayMs))
            {
                return false;
            }

            useRandomDelay = true;
            return true;
        }

        if (tokens.Length is 3 && tokens[1].Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            var rangeTokens = tokens[2].Split("..", 2, StringSplitOptions.TrimEntries);
            if (rangeTokens.Length is not 2
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
        if ((tokens.Length is not (2 or 3)) || !tokens[0].Equals("scroll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parsedAmount = 1;
        if (tokens.Length is 3
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

        if (step.Length is 4)
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

    private static bool TryParseClipboardStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptSyntax.StartsWithCommandToken(step.TrimStart(), RunScriptSyntax.ClipboardCommand))
        {
            return false;
        }

        var trimmed = step.Trim();
        var parts = trimmed.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 3 || !parts[0].Equals(RunScriptSyntax.ClipboardCommand, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts[1].Equals("get", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryNormalizeVariableName(parts[2], out var variableName))
            {
                return false;
            }

            action = new EditorAction
            {
                Type = EditorActionType.ClipboardGet,
                ScriptVariableName = variableName,
            };
            return true;
        }

        if (parts[1].Equals("set", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(parts[2]))
        {
            action = new EditorAction
            {
                Type = EditorActionType.ClipboardSet,
                Text = parts[2],
            };
            return true;
        }

        return false;
    }

    private static bool TryParseShellStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptSyntax.IsShellStep(step))
        {
            return false;
        }

        var payload = step.Trim()["shell".Length..].TrimStart();
        if (payload.Length is 0)
        {
            return false;
        }

        if (TryConsumeShellMode(payload, "capture-input", out var afterCaptureInput))
        {
            return TryParseShellCaptureInputStep(afterCaptureInput, out action);
        }

        if (TryConsumeShellMode(payload, "capture", out var afterCapture))
        {
            return TryParseShellCaptureStep(afterCapture, out action);
        }

        if (TryConsumeShellMode(payload, "input", out var afterInput))
        {
            return TryParseShellInputStep(afterInput, out action);
        }

        return TryParseShellRunStep(payload, out action);
    }

    private static bool TryParseScreenshotStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptPlatformSyntax.IsScreenshotStep(step))
        {
            return false;
        }

        if (!RunScriptPlatformSyntax.TryParseScreenshotStep(step, out var parsed, out _))
        {
            return false;
        }

        action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotOutputPath = parsed.OutputPath ?? string.Empty,
            ScreenshotCopyToClipboard = parsed.CopyToClipboard,
            ScreenshotUseRegion = parsed.UseRegion,
            ScreenshotRegionX = parsed.UseRegion ? parsed.RegionX : "0",
            ScreenshotRegionY = parsed.UseRegion ? parsed.RegionY : "0",
            ScreenshotRegionWidth = parsed.UseRegion ? parsed.RegionWidth : "100",
            ScreenshotRegionHeight = parsed.UseRegion ? parsed.RegionHeight : "100",
        };
        return true;
    }

    private static bool TryParseWindowStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        var trimmed = step.Trim();
        var validationError = Playback.RunScriptWindowExecutor.Validate(trimmed);
        if (validationError is not null)
        {
            return false;
        }

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !parts[0].Equals("window", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        switch (parts[1].ToLowerInvariant())
        {
            case "active":
                action = CreateWindowAction(WindowCommandMode.Active, activeField: parts[2], outputVariable: parts[3]);
                return true;
            case "search":
                return TryParseWindowSearch(trimmed, out action);
            case "wait":
                return TryParseWindowWait(trimmed, out action);
            case "focus":
                return TryParseWindowSelectorCommand(trimmed, WindowCommandMode.Focus, out action);
            case "close":
                return TryParseWindowSelectorCommand(trimmed, WindowCommandMode.Close, out action);
            case "move":
                action = CreateWindowAction(WindowCommandMode.Move, x: int.Parse(parts[2], CultureInfo.InvariantCulture), y: int.Parse(parts[3], CultureInfo.InvariantCulture));
                return true;
            case "resize":
                action = CreateWindowAction(WindowCommandMode.Resize, width: int.Parse(parts[2], CultureInfo.InvariantCulture), height: int.Parse(parts[3], CultureInfo.InvariantCulture));
                return true;
            case "center":
                action = CreateWindowAction(WindowCommandMode.Center);
                return true;
            case "maximize":
                action = CreateWindowAction(WindowCommandMode.Maximize);
                return true;
            case "fullscreen":
                action = CreateWindowAction(WindowCommandMode.Fullscreen);
                return true;
            case "float":
                action = CreateWindowAction(WindowCommandMode.Floating);
                return true;
            case "getdesktop":
                action = CreateWindowAction(WindowCommandMode.WorkspaceGet, outputVariable: parts[2]);
                return true;
            case "setdesktop":
                action = CreateWindowAction(WindowCommandMode.WorkspaceSwitch, workspace: UnquoteWindowField(string.Join(' ', parts[2..])));
                return true;
            case "setdesktopforwindow":
                return TryParseWindowWorkspaceMove(parts, out action);
            default:
                return false;
        }
    }

    private static bool TryParseWindowSearch(string step, out EditorAction action)
    {
        action = new EditorAction();
        var parts = step.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 4 || !TryExtractLastToken(parts[3], out var rawTerm, out var outputVariable))
        {
            return false;
        }

        action = CreateWindowAction(WindowCommandMode.Search, selectorKind: parts[2], selectorValue: UnquoteWindowField(rawTerm), outputVariable: outputVariable);
        return true;
    }

    private static bool TryParseWindowWait(string step, out EditorAction action)
    {
        action = new EditorAction();
        var parts = step.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 4 || !TryExtractLastToken(parts[3], out var beforeVariable, out var outputVariable))
        {
            return false;
        }

        var timeoutMs = 5000;
        var rawTerm = beforeVariable;
        if (TryExtractLastToken(beforeVariable, out var beforeTimeout, out var maybeTimeout)
            && int.TryParse(maybeTimeout, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedTimeout)
            && parsedTimeout > 0)
        {
            timeoutMs = parsedTimeout;
            rawTerm = beforeTimeout;
        }

        action = CreateWindowAction(WindowCommandMode.Wait, selectorKind: parts[2], selectorValue: UnquoteWindowField(rawTerm), outputVariable: outputVariable, timeoutMs: timeoutMs);
        return true;
    }

    private static bool TryParseWindowSelectorCommand(string step, WindowCommandMode mode, out EditorAction action)
    {
        action = new EditorAction();
        var parts = step.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        var selectorKind = parts[2].ToLowerInvariant();
        var selectorValue = selectorKind is "active" ? string.Empty : UnquoteWindowField(parts.Length is 4 ? parts[3] : string.Empty);
        action = CreateWindowAction(mode, selectorKind: selectorKind, selectorValue: selectorValue);
        return true;
    }

    private static bool TryParseWindowWorkspaceMove(string[] parts, out EditorAction action)
    {
        action = new EditorAction();
        if (parts.Length < 4)
        {
            return false;
        }

        var selectorKind = parts[2].ToLowerInvariant();
        if (selectorKind is "active")
        {
            action = CreateWindowAction(WindowCommandMode.WorkspaceMoveActive, workspace: UnquoteWindowField(string.Join(' ', parts[3..])));
            return true;
        }

        if (selectorKind is "address" && parts.Length >= 5)
        {
            action = CreateWindowAction(WindowCommandMode.WorkspaceMoveWindow, selectorKind: "address", selectorValue: parts[3], workspace: UnquoteWindowField(string.Join(' ', parts[4..])));
            return true;
        }

        return false;
    }

    private static bool TryExtractLastToken(string value, out string beforeLast, out string lastToken)
    {
        beforeLast = string.Empty;
        lastToken = string.Empty;
        var lastSpace = value.Trim().LastIndexOf(' ');
        if (lastSpace < 0)
        {
            return false;
        }

        beforeLast = value[..lastSpace].Trim();
        lastToken = value[(lastSpace + 1)..].Trim();
        return beforeLast.Length > 0 && lastToken.Length > 0;
    }

    private static string UnquoteWindowField(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length < 2 || !((trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            return trimmed;
        }

        var quote = trimmed[0];
        var builder = new StringBuilder();
        for (var index = 1; index < trimmed.Length - 1; index++)
        {
            if (trimmed[index] == '\\' && index + 1 < trimmed.Length - 1 && (trimmed[index + 1] == quote || trimmed[index + 1] == '\\'))
            {
                builder.Append(trimmed[index + 1]);
                index++;
                continue;
            }

            builder.Append(trimmed[index]);
        }

        return builder.ToString();
    }

    private static EditorAction CreateWindowAction(
        WindowCommandMode mode,
        string selectorKind = "title",
        string selectorValue = "",
        string activeField = "title",
        string outputVariable = "windowResult",
        int timeoutMs = 5000,
        int x = 0,
        int y = 0,
        int width = 1280,
        int height = 720,
        string workspace = "")
    {
        return new EditorAction
        {
            Type = EditorActionType.WindowCommand,
            WindowCommandMode = mode,
            WindowSelectorKind = selectorKind,
            WindowSelectorValue = selectorValue,
            WindowActiveField = activeField,
            WindowOutputVariable = EditorActionScriptTokens.NormalizeVariableToken(outputVariable),
            WindowTimeoutMs = timeoutMs,
            WindowX = x,
            WindowY = y,
            WindowWidth = width,
            WindowHeight = height,
            WindowWorkspace = workspace,
        };
    }

    private static bool TryParseShellRunStep(string payload, out EditorAction action)
    {
        action = new EditorAction();
        if (!TryReadQuotedShellField(payload, allowEmpty: false, out var command, out var afterCommand)
            || !TryParseShellOptions(afterCommand, out var retries, out var backoffMs, out var timeoutMs))
        {
            return false;
        }

        action = CreateShellAction(ShellCommandMode.Shell, command, string.Empty, exitVariable: null, stdoutVariable: null, stderrVariable: null, retries, backoffMs, timeoutMs);
        return true;
    }

    private static bool TryParseShellCaptureStep(string payload, out EditorAction action)
    {
        action = new EditorAction();
        if (!TryReadQuotedShellField(payload, allowEmpty: false, out var command, out var afterCommand)
            || !TryReadShellCaptureTargets(afterCommand, out var exitVariable, out var stdoutVariable, out var stderrVariable, out var optionText)
            || !TryParseShellOptions(optionText, out var retries, out var backoffMs, out var timeoutMs))
        {
            return false;
        }

        action = CreateShellAction(ShellCommandMode.ShellCapture, command, string.Empty, exitVariable, stdoutVariable, stderrVariable, retries, backoffMs, timeoutMs);
        return true;
    }

    private static bool TryParseShellInputStep(string payload, out EditorAction action)
    {
        action = new EditorAction();
        if (!TryReadQuotedShellField(payload, allowEmpty: true, out var standardInput, out var afterInput)
            || !TryReadQuotedShellField(afterInput, allowEmpty: false, out var command, out var afterCommand)
            || !TryParseShellOptions(afterCommand, out var retries, out var backoffMs, out var timeoutMs))
        {
            return false;
        }

        action = CreateShellAction(ShellCommandMode.ShellInput, command, standardInput, exitVariable: null, stdoutVariable: null, stderrVariable: null, retries, backoffMs, timeoutMs);
        return true;
    }

    private static bool TryParseShellCaptureInputStep(string payload, out EditorAction action)
    {
        action = new EditorAction();
        if (!TryReadQuotedShellField(payload, allowEmpty: true, out var standardInput, out var afterInput)
            || !TryReadQuotedShellField(afterInput, allowEmpty: false, out var command, out var afterCommand)
            || !TryReadShellCaptureTargets(afterCommand, out var exitVariable, out var stdoutVariable, out var stderrVariable, out var optionText)
            || !TryParseShellOptions(optionText, out var retries, out var backoffMs, out var timeoutMs))
        {
            return false;
        }

        action = CreateShellAction(ShellCommandMode.ShellCaptureInput, command, standardInput, exitVariable, stdoutVariable, stderrVariable, retries, backoffMs, timeoutMs);
        return true;
    }

    private static bool TryConsumeShellMode(string payload, string mode, out string remaining)
    {
        remaining = string.Empty;
        if (!payload.StartsWith(mode, StringComparison.OrdinalIgnoreCase)
            || (payload.Length != mode.Length && !char.IsWhiteSpace(payload[mode.Length])))
        {
            return false;
        }

        remaining = payload[mode.Length..].TrimStart();
        return true;
    }

    private static bool TryReadQuotedShellField(string payload, bool allowEmpty, out string value, out string remaining)
    {
        value = string.Empty;
        remaining = string.Empty;
        var trimmed = payload.TrimStart();
        if (trimmed.Length is 0 || trimmed[0] is not ('\"' or '\''))
        {
            return false;
        }

        var quote = trimmed[0];
        var builder = new StringBuilder();
        for (var index = 1; index < trimmed.Length; index++)
        {
            var current = trimmed[index];
            if (current == '\\' && index + 1 < trimmed.Length && (trimmed[index + 1] == quote || trimmed[index + 1] == '\\'))
            {
                builder.Append(trimmed[index + 1]);
                index++;
                continue;
            }

            if (current == quote)
            {
                if (index + 1 < trimmed.Length && !char.IsWhiteSpace(trimmed[index + 1]))
                {
                    return false;
                }

                value = builder.ToString();
                remaining = trimmed[(index + 1)..].TrimStart();
                return allowEmpty || !string.IsNullOrWhiteSpace(value);
            }

            builder.Append(current);
        }

        return false;
    }

    private static bool TryReadShellCaptureTargets(string payload, out string exitVariable, out string stdoutVariable, out string stderrVariable, out string optionText)
    {
        exitVariable = string.Empty;
        stdoutVariable = string.Empty;
        stderrVariable = string.Empty;
        optionText = string.Empty;
        var tokens = SplitShellTokens(payload);
        if (tokens.Length < 3
            || !TryNormalizeShellCaptureTarget(tokens[0], out exitVariable)
            || !TryNormalizeShellCaptureTarget(tokens[1], out stdoutVariable)
            || !TryNormalizeShellCaptureTarget(tokens[2], out stderrVariable))
        {
            return false;
        }

        optionText = string.Join(' ', tokens[3..]);
        return true;
    }

    private static bool TryNormalizeShellCaptureTarget(string token, out string target)
    {
        target = token;
        if (token is "_")
        {
            return true;
        }

        return TryNormalizeVariableName(token, out target);
    }

    private static bool TryParseShellOptions(string payload, out int retries, out int backoffMs, out int timeoutMs)
    {
        retries = 0;
        backoffMs = 0;
        timeoutMs = 0;
        var tokens = SplitShellTokens(payload);
        if (tokens.Length > 3)
        {
            return false;
        }

        var values = new[] { 0, 0, 0 };
        for (var index = 0; index < tokens.Length; index++)
        {
            if (!int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
|| value < 0
|| (index is 0 && value > 10_000))
            {
                return false;
            }

            values[index] = value;
        }

        retries = values[0];
        backoffMs = values[1];
        timeoutMs = values[2];
        return true;
    }

    private static string[] SplitShellTokens(string payload)
    {
        return payload.Trim().Length is 0
            ? []
            : payload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static EditorAction CreateShellAction(
        ShellCommandMode mode,
        string command,
        string standardInput,
        string? exitVariable,
        string? stdoutVariable,
        string? stderrVariable,
        int retries,
        int backoffMs,
        int timeoutMs)
    {
        return new EditorAction
        {
            Type = EditorActionType.ShellCommand,
            ShellCommandMode = mode,
            ShellCommand = command,
            ShellStandardInput = standardInput,
            ShellExitCodeVariableName = exitVariable ?? "exit_code",
            ShellStandardOutputVariableName = stdoutVariable ?? "stdout",
            ShellStandardErrorVariableName = stderrVariable ?? "stderr",
            ShellRetries = retries,
            ShellBackoffMs = backoffMs,
            ShellTimeoutMs = timeoutMs,
        };
    }

    private static bool TryParseSetStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!step.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[4..].Trim();
        if (payload.Length is 0)
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
                ScriptValue = value,
            };
            return true;
        }

        action = new EditorAction
        {
            Type = EditorActionType.SetVariable,
            Text = payload,
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
            || TryParsePixelSearchStep(step, out action)
            || TryParseImageSearchStep(step, out action)
            || TryParseImageClickStep(step, out action)
            || TryParseWaitImageStep(step, out action);
    }

    private static bool TryParsePixelColorStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptScreenReadingStepParser.TryParseCommand(step, out var command, out var tokens)
|| command is not RunScriptScreenReadingCommand.PixelColor)
        {
            return false;
        }

        var isRelative = tokens.Length > 1 && tokens[1].Equals("rel", StringComparison.OrdinalIgnoreCase);
        var coordinateIndex = isRelative ? 2 : 1;
        if (tokens.Length < coordinateIndex + 2)
        {
            return false;
        }

        if (!TryParseInteger(tokens[coordinateIndex], out var x)
            || !TryParseInteger(tokens[coordinateIndex + 1], out var y))
        {
            return false;
        }

        var index = coordinateIndex + 2;
        var variableName = EditorActionScreenReadingPayload.DefaultColorVariableName;
        if (index < tokens.Length && !RunScriptScreenReadingStepParser.IsScreenReadTimeoutKeyword(tokens[index]))
        {
            if (!TryNormalizeVariableName(tokens[index], out variableName))
            {
                return false;
            }

            index++;
        }

        var timeoutMs = EditorActionScreenReadingPayload.DefaultTimeoutMs;
        if (!TryParseScreenReadTimeout(tokens, index, ref timeoutMs))
        {
            return false;
        }

        action = new EditorAction();
        action.ApplyScreenReadingPayload(EditorActionScreenReadingPayload.ForPixelColor(!isRelative, x, y, variableName));
        action.ScreenTimeoutMs = timeoutMs;
        return true;
    }

    private static bool TryParseWaitColorStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptScreenReadingStepParser.TryParseCommand(step, out var command, out var tokens)
|| command is not RunScriptScreenReadingCommand.WaitColor
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

        var variableName = tokens.Length is 6 && TryNormalizeVariableName(tokens[5], out var resultVariableName)
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
|| command is not RunScriptScreenReadingCommand.PixelSearch
|| tokens.Length < 6)
        {
            return false;
        }

        if (!TryParseInteger(tokens[1], out var x1)
            || !TryParseInteger(tokens[2], out var y1)
            || !TryParseInteger(tokens[3], out var x2)
            || !TryParseInteger(tokens[4], out var y2)
            || !TryParseTargetColorToken(tokens[5], out var colorSource, out var colorHex, out var targetColorVariableName)
            || !TryGetPositiveRegionSize(x1, y1, x2, y2, out var width, out var height))
        {
            return false;
        }

        var index = 6;
        var variables = new List<string>(3);
        while (index < tokens.Length && !RunScriptScreenReadingStepParser.IsPixelSearchOptionKeyword(tokens[index]))
        {
            if (!TryNormalizeVariableName(tokens[index], out var variableName))
            {
                return false;
            }

            variables.Add(variableName);
            index++;
        }

        if (variables.Count is not 0 and not 2 and not 3)
        {
            return false;
        }

        var foundName = variables.Count is 3 ? variables[0] : EditorActionScreenReadingPayload.DefaultFoundVariableName;
        var xVariableName = variables.Count is 3
            ? variables[1]
            : variables.Count is 2 ? variables[0] : EditorActionScreenReadingPayload.DefaultFoundXVariableName;
        var yVariableName = variables.Count is 3
            ? variables[2]
            : variables.Count is 2 ? variables[1] : EditorActionScreenReadingPayload.DefaultFoundYVariableName;
        var tolerance = 0;
        var timeoutMs = EditorActionScreenReadingPayload.DefaultTimeoutMs;
        while (index < tokens.Length)
        {
            if (RunScriptScreenReadingStepParser.IsScreenReadTimeoutKeyword(tokens[index]))
            {
                if (index + 1 >= tokens.Length || !TryParseInteger(tokens[index + 1], out timeoutMs))
                {
                    return false;
                }

                index += 2;
                continue;
            }

            if (RunScriptScreenReadingStepParser.IsPixelSearchToleranceKeyword(tokens[index]))
            {
                if (index + 1 >= tokens.Length || !TryParseInteger(tokens[index + 1], out tolerance) || tolerance is < 0 or > byte.MaxValue)
                {
                    return false;
                }

                index += 2;
                continue;
            }

            return false;
        }

        action = new EditorAction();
        action.ApplyScreenReadingPayload(EditorActionScreenReadingPayload.ForPixelSearch(
            x1,
            y1,
            width,
            height,
            colorHex,
            foundName,
            xVariableName,
            yVariableName,
            tolerance));
        action.ScreenTimeoutMs = timeoutMs;
        action.ScreenTargetColorSource = colorSource;
        action.ScreenTargetColorVariableName = targetColorVariableName;
        return true;
    }

    private static bool TryGetPositiveRegionSize(int left, int top, int right, int bottom, out int width, out int height)
    {
        var widthValue = (long)right - left;
        var heightValue = (long)bottom - top;
        if (widthValue <= 0 || heightValue <= 0 || widthValue > int.MaxValue || heightValue > int.MaxValue)
        {
            width = 0;
            height = 0;
            return false;
        }

        width = (int)widthValue;
        height = (int)heightValue;
        return true;
    }

    private static bool TryParseScreenReadTimeout(IReadOnlyList<string> tokens, int startIndex, ref int timeoutMs)
    {
        var hasTimeout = false;
        for (var index = startIndex; index < tokens.Count;)
        {
            if (!RunScriptScreenReadingStepParser.IsScreenReadTimeoutKeyword(tokens[index])
                || hasTimeout
                || index + 1 >= tokens.Count
                || !TryParseInteger(tokens[index + 1], out timeoutMs)
                || timeoutMs < 0)
            {
                return false;
            }

            hasTimeout = true;
            index += 2;
        }

        return true;
    }

    private static bool TryParseImageSearchStep(string step, out EditorAction action)
    {
        return TryParseImageActionStep(step, RunScriptScreenReadingCommand.ImageSearch, EditorActionType.ImageSearch, out action);
    }

    private static bool TryParseImageClickStep(string step, out EditorAction action)
    {
        return TryParseImageActionStep(step, RunScriptScreenReadingCommand.ImageClick, EditorActionType.ImageClick, out action);
    }

    private static bool TryParseWaitImageStep(string step, out EditorAction action)
    {
        return TryParseImageActionStep(step, RunScriptScreenReadingCommand.WaitImage, EditorActionType.WaitImage, out action);
    }

    private static bool TryParseImageActionStep(string step, RunScriptScreenReadingCommand expectedCommand, EditorActionType actionType, out EditorAction action)
    {
        action = new EditorAction();
        if (!RunScriptScreenReadingStepParser.TryParseCommand(step, out var command, out var tokens)
|| command != expectedCommand
|| !RunScriptScreenReadingStepParser.TryValidateStep(step, out var error)
|| error is not null)
        {
            return false;
        }

        var left = 0;
        var top = 0;
        var right = 0;
        var bottom = 0;
        var hasRegion = tokens.Length >= 6
            && TryParseInteger(tokens[1], out left)
            && TryParseInteger(tokens[2], out top)
            && TryParseInteger(tokens[3], out right)
            && TryParseInteger(tokens[4], out bottom);
        var regionWidth = 0;
        var regionHeight = 0;
        if (hasRegion && !TryGetPositiveRegionSize(left, top, right, bottom, out regionWidth, out regionHeight))
        {
            return false;
        }

        var imageNameIndex = hasRegion ? 5 : 1;
        if (!TryNormalizeVariableName(tokens[imageNameIndex], out var imageName))
        {
            return false;
        }

        var optionIndex = imageNameIndex + 1;
        var variableNames = new List<string>(3);
        if (actionType is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage)
        {
            while (optionIndex < tokens.Length && !IsImageActionOptionKeyword(actionType, tokens[optionIndex]))
            {
                if (!TryNormalizeVariableName(tokens[optionIndex], out var variableName))
                {
                    return false;
                }

                variableNames.Add(variableName);
                optionIndex++;
            }

            if (variableNames.Count is not 0 and not 3)
            {
                return false;
            }
        }

        var similarity = 1.0;
        var downsample = 1;
        var matchMode = EditorImageMatchMode.FirstThresholdMatch;
        var matchModeExplicit = false;
        var scaleAware = false;
        var timeoutMs = EditorActionScreenReadingPayload.DefaultTimeoutMs;
        var button = MacroMouseButton.Left;
        for (var index = optionIndex; index < tokens.Length;)
        {
            if (RunScriptSyntax.IsImageSearchSimilarityKeyword(tokens[index]))
            {
                if (!double.TryParse(tokens[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out similarity)
                    || !double.IsFinite(similarity)
                    || similarity is < 0.0 or > 1.0)
                {
                    return false;
                }

                index += 2;
                continue;
            }

            if (RunScriptSyntax.IsImageSearchDownsampleKeyword(tokens[index]))
            {
                if (!TryParseInteger(tokens[index + 1], out downsample))
                {
                    return false;
                }

                index += 2;
                continue;
            }

            if (RunScriptPlatformSyntax.IsImageSearchMatchModeKeyword(tokens[index]))
            {
                if (index + 1 >= tokens.Length || !RunScriptPlatformSyntax.TryParseImageMatchMode(tokens[index + 1], out matchMode))
                {
                    return false;
                }

                matchModeExplicit = true;

                index += 2;
                continue;
            }

            if (RunScriptSyntax.IsImageSearchScaleAwareKeyword(tokens[index]))
            {
                scaleAware = true;
                index++;
                continue;
            }

            if (RunScriptSyntax.IsImageSearchTimeoutKeyword(tokens[index]))
            {
                if (!TryParseInteger(tokens[index + 1], out timeoutMs))
                {
                    return false;
                }

                index += 2;
                continue;
            }

            if (actionType is EditorActionType.ImageClick
&& string.Equals(tokens[index], "button", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseButtonToken(tokens[index + 1], out button)
                    || button is not (MacroMouseButton.Left or MacroMouseButton.Right or MacroMouseButton.Middle))
                {
                    return false;
                }

                index += 2;
                continue;
            }

            return false;
        }

        action = new EditorAction
        {
            Type = actionType,
            ScreenLeft = hasRegion ? left : 0,
            ScreenTop = hasRegion ? top : 0,
            ScreenWidth = hasRegion ? regionWidth : EditorActionScreenReadingPayload.DefaultSearchScreenWidth,
            ScreenHeight = hasRegion ? regionHeight : EditorActionScreenReadingPayload.DefaultSearchScreenHeight,
            ImageAssetName = imageName,
            ScreenFoundVariableName = variableNames.Count is 3 ? variableNames[0] : EditorActionScreenReadingPayload.DefaultFoundVariableName,
            ScreenFoundXVariableName = variableNames.Count is 3 ? variableNames[1] : EditorActionScreenReadingPayload.DefaultFoundXVariableName,
            ScreenFoundYVariableName = variableNames.Count is 3 ? variableNames[2] : EditorActionScreenReadingPayload.DefaultFoundYVariableName,
            ScreenTimeoutMs = timeoutMs,
            ImageSearchSimilarity = similarity,
            ImageSearchDownsample = downsample,
            ImageSearchMatchMode = matchMode,
            ImageSearchMatchModeWasExplicit = matchModeExplicit,
            ImageSearchScaleAware = scaleAware,
            Button = actionType is EditorActionType.ImageClick ? button : MacroMouseButton.Left,
        };
        return true;
    }

    private static bool IsImageActionOptionKeyword(EditorActionType actionType, string token)
    {
        return RunScriptScreenReadingStepParser.IsImageSearchOptionKeyword(token)
|| (actionType is EditorActionType.ImageClick && string.Equals(token, "button", StringComparison.OrdinalIgnoreCase));
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

        if (!token.StartsWith('$')
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
        if (payload.Length is 0)
        {
            return false;
        }

        var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is 0)
        {
            return false;
        }

        if (!TryNormalizeVariableName(parts[0], out var variableName))
        {
            action = new EditorAction
            {
                Type = actionType,
                Text = payload,
            };
            return true;
        }

        var amountToken = parts.Length > 1 ? parts[1] : "1";
        if (!TryParseNumericToken(amountToken, out var sourceType, out var tokenValue))
        {
            action = new EditorAction
            {
                Type = actionType,
                Text = payload,
            };
            return true;
        }

        action = new EditorAction
        {
            Type = actionType,
            ScriptVariableName = variableName,
            ScriptNumericSourceType = sourceType,
            ScriptNumericValue = tokenValue,
        };
        return true;
    }

    private static bool TryParseRepeatStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!step.StartsWith("repeat ", StringComparison.OrdinalIgnoreCase)
            || !step.EndsWith('{'))
        {
            return false;
        }

        var token = step[7..^1].Trim();
        if (token.Length is 0)
        {
            return false;
        }

        if (TryParseNumericToken(token, out var sourceType, out var tokenValue))
        {
            action = new EditorAction
            {
                Type = EditorActionType.RepeatBlockStart,
                ScriptNumericSourceType = sourceType,
                ScriptNumericValue = tokenValue,
            };
            return true;
        }

        action = new EditorAction
        {
            Type = EditorActionType.RepeatBlockStart,
            Text = token,
        };
        return true;
    }

    private static bool TryParseConditionStep(string step, string keyword, EditorActionType actionType, out EditorAction action)
    {
        action = new EditorAction();
        if (!step.StartsWith($"{keyword} ", StringComparison.OrdinalIgnoreCase)
            || !step.EndsWith('{'))
        {
            return false;
        }

        var condition = step[(keyword.Length + 1)..^1].Trim();
        if (condition.Length is 0)
        {
            return false;
        }

        if (RunScriptConditionParser.TryParse(condition, out var parsedCondition, out _)
            && parsedCondition != null
            && TryMapConditionOperatorToken(parsedCondition.OperatorToken, out var conditionOperator))
        {
            var preferColor = conditionOperator is ScriptConditionOperator.Equals or ScriptConditionOperator.NotEquals;
            if (!TryParseOperandToken(parsedCondition.LeftToken, out var leftType, out var leftValue, preferColor)
                || !TryParseOperandToken(parsedCondition.RightToken, out var rightType, out var rightValue, preferColor))
            {
                return false;
            }

            action = new EditorAction
            {
                Type = actionType,
                ScriptLeftOperandType = leftType,
                ScriptLeftOperand = leftValue,
                ScriptConditionOperator = conditionOperator,
                ScriptRightOperandType = rightType,
                ScriptRightOperand = rightValue,
            };
            return true;
        }

        action = new EditorAction
        {
            Type = actionType,
            Text = condition,
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
            _ => ScriptConditionOperator.Equals,
        };

        return operatorToken is "==" or "!=" or ">" or ">=" or "<" or "<=";
    }

    private static bool TryParseForStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        if (!step.StartsWith("for ", StringComparison.OrdinalIgnoreCase)
            || !step.EndsWith('{'))
        {
            return false;
        }

        var body = step[4..^1].Trim();
        if (body.Length is 0)
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
                Text = body,
            };
            return true;
        }

        if (tokens.Length is not (5 or 7))
        {
            action = new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                Text = body,
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
                Text = body,
            };
            return true;
        }

        var hasStep = false;
        var stepType = ScriptNumericSourceType.Number;
        var stepValue = "1";
        if (tokens.Length is 7)
        {
            if (!tokens[5].Equals("step", StringComparison.OrdinalIgnoreCase)
                || !TryParseNumericToken(tokens[6], out stepType, out stepValue))
            {
                action = new EditorAction
                {
                    Type = EditorActionType.ForBlockStart,
                    Text = body,
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
            ForStepValue = stepValue,
        };
        return true;
    }

    private static bool TryParseNumericToken(string rawToken, out ScriptNumericSourceType sourceType, out string tokenValue)
    {
        sourceType = ScriptNumericSourceType.Number;
        tokenValue = string.Empty;

        var token = rawToken.Trim();
        if (token.Length is 0)
        {
            return false;
        }

        if (token.StartsWith('$'))
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

    private static bool TryParseOperandToken(
        string rawToken,
        out ScriptOperandType operandType,
        out string tokenValue,
        bool preferColor = false)
    {
        operandType = ScriptOperandType.Text;
        tokenValue = string.Empty;

        var token = rawToken.Trim();
        if (token.Length is 0)
        {
            return false;
        }

        if (token.StartsWith("$$", StringComparison.Ordinal))
        {
            operandType = ScriptOperandType.Text;
            tokenValue = EditorActionScriptTokens.UnescapeLiteralDollar(token);
            return true;
        }

        if (token.StartsWith('$'))
        {
            var variable = token[1..].Trim();
            if (!TryNormalizeVariableName(variable, out tokenValue))
            {
                return false;
            }

            operandType = ScriptOperandType.VariableReference;
            return true;
        }

        if (preferColor && ScreenPixelColor.TryParse(token, out var color))
        {
            operandType = ScriptOperandType.Color;
            tokenValue = color.ToString();
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

        if (ScreenPixelColor.TryParse(token, out color))
        {
            operandType = ScriptOperandType.Color;
            tokenValue = color.ToString();
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
        if (token.Length is 0)
        {
            return false;
        }

        if (token.StartsWith("$$", StringComparison.Ordinal))
        {
            valueType = ScriptValueType.Text;
            value = EditorActionScriptTokens.UnescapeLiteralDollar(token);
            return true;
        }

        if (token.StartsWith('$'))
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
                UseRandomDelay = false,
            });
        }

        if (hasRandomDelay)
        {
            actions.Add(new EditorAction
            {
                Type = EditorActionType.Delay,
                UseRandomDelay = true,
                RandomDelayMinMs = randomDelayMinMs,
                RandomDelayMaxMs = randomDelayMaxMs,
            });
        }
    }

    private static bool IsShiftKey(int keyCode)
    {
        return keyCode is InputEventCode.KEY_LEFTSHIFT or InputEventCode.KEY_RIGHTSHIFT;
    }

    private static bool IsScrollButton(MacroMouseButton button)
    {
        return button is MacroMouseButton.ScrollUp or MacroMouseButton.ScrollDown
            or MacroMouseButton.ScrollLeft or MacroMouseButton.ScrollRight;
    }
}
