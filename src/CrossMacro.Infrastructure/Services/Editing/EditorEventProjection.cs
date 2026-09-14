namespace CrossMacro.Infrastructure.Services.Editing;

internal sealed class EditorEventProjection
{
    private readonly IKeyCodeMapper _keyCodeMapper;
    internal EditorEventProjection(IKeyCodeMapper keyCodeMapper)
    {
        _keyCodeMapper = keyCodeMapper;
    }

    internal IReadOnlyList<MacroEvent> ToMacroEvents(EditorAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var events = new List<MacroEvent>();
        var x = action.X;
        var y = action.Y;
        if (UsesPositionCoordinates(action) && !action.TryGetLiteralCoordinates(out x, out y))
        {
            throw new InvalidOperationException("Variable mouse coordinates require script-backed sequence conversion.");
        }

        switch (action.Type)
        {
            case EditorActionType.MouseMove:
                events.Add(new MacroEvent { Type = EventType.MouseMove, X = x, Y = y, DelayMicroseconds = action.DelayMicroseconds, CoordinateMode = action.IsAbsolute ? MouseCoordinateMode.Absolute : MouseCoordinateMode.Relative, CoordinateSpace = action.IsAbsolute ? MouseCoordinateSpace.LogicalDesktop : action.CoordinateSpace, });
                break;
            case EditorActionType.MouseClick:
                events.Add(new MacroEvent { Type = EventType.Click, X = action.UseCurrentPosition ? 0 : x, Y = action.UseCurrentPosition ? 0 : y, Button = action.Button, DelayMicroseconds = action.DelayMicroseconds, UseCurrentPosition = action.UseCurrentPosition, CoordinateMode = GetCoordinateMode(action), CoordinateSpace = GetCoordinateSpace(action), });
                break;
            case EditorActionType.MouseDown:
                events.Add(new MacroEvent { Type = EventType.ButtonPress, X = action.UseCurrentPosition ? 0 : x, Y = action.UseCurrentPosition ? 0 : y, Button = action.Button, DelayMicroseconds = action.DelayMicroseconds, UseCurrentPosition = action.UseCurrentPosition, CoordinateMode = GetCoordinateMode(action), CoordinateSpace = GetCoordinateSpace(action), });
                break;
            case EditorActionType.MouseUp:
                events.Add(new MacroEvent { Type = EventType.ButtonRelease, X = action.UseCurrentPosition ? 0 : x, Y = action.UseCurrentPosition ? 0 : y, Button = action.Button, DelayMicroseconds = action.DelayMicroseconds, UseCurrentPosition = action.UseCurrentPosition, CoordinateMode = GetCoordinateMode(action), CoordinateSpace = GetCoordinateSpace(action), });
                break;
            case EditorActionType.KeyPress:
                // KeyPress expands to KeyDown + KeyUp
                events.Add(new MacroEvent { Type = EventType.KeyPress, KeyCode = action.KeyCode, DelayMicroseconds = action.DelayMicroseconds, });
                events.Add(new MacroEvent { Type = EventType.KeyRelease, KeyCode = action.KeyCode, DelayMicroseconds = MacroTiming.DefaultKeyPressDelayMicroseconds, });
                break;
            case EditorActionType.KeyDown:
                events.Add(new MacroEvent { Type = EventType.KeyPress, KeyCode = action.KeyCode, DelayMicroseconds = action.DelayMicroseconds, });
                break;
            case EditorActionType.KeyUp:
                events.Add(new MacroEvent { Type = EventType.KeyRelease, KeyCode = action.KeyCode, DelayMicroseconds = action.DelayMicroseconds, });
                break;
            case EditorActionType.Delay:
                // Delay is added to the next event's DelayMs
                // Create a placeholder move event with the delay
                events.Add(new MacroEvent { Type = EventType.None, DelayMicroseconds = action.UseRandomDelay ? 0 : action.DelayMicroseconds, HasRandomDelay = action.UseRandomDelay, RandomDelayMinMs = action.UseRandomDelay ? action.RandomDelayMinMs : 0, RandomDelayMaxMs = action.UseRandomDelay ? action.RandomDelayMaxMs : 0, });
                break;
            case EditorActionType.ScrollVertical:
                var scrollButton = action.ScrollAmount > 0 ? MacroMouseButton.ScrollUp : MacroMouseButton.ScrollDown;
                for (int i = 0; i < Math.Abs(action.ScrollAmount); i++)
                {
                    events.Add(new MacroEvent { Type = EventType.Click, Button = scrollButton, DelayMicroseconds = i is 0 ? action.DelayMicroseconds : 0, });
                }

                break;
            case EditorActionType.ScrollHorizontal:
                var hScrollButton = action.ScrollAmount > 0 ? MacroMouseButton.ScrollRight : MacroMouseButton.ScrollLeft;
                for (int i = 0; i < Math.Abs(action.ScrollAmount); i++)
                {
                    events.Add(new MacroEvent { Type = EventType.Click, Button = hScrollButton, DelayMicroseconds = i is 0 ? action.DelayMicroseconds : 0, });
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
                        AddKeyStroke(events, InputEventCode.KEY_ENTER, ref isFirst, action.DelayMicroseconds);
                        continue;
                    }

                    if (TryGetTextInputControlKeyCode(c, out var controlKeyCode))
                    {
                        AddKeyStroke(events, controlKeyCode, ref isFirst, action.DelayMicroseconds);
                        continue;
                    }

                    var keyCode = _keyCodeMapper.GetKeyCodeForCharacter(c);
                    if (keyCode == -1)
                    {
                        continue; // Skip unmappable characters
                    }

                    var needsShift = _keyCodeMapper.RequiresShift(c);
                    var needsAltGr = _keyCodeMapper.RequiresAltGr(c);
                    AddKeyStroke(events, keyCode, ref isFirst, action.DelayMicroseconds, needsShift, needsAltGr);
                }

                break;
            case EditorActionType.SetVariable:
            case EditorActionType.IncrementVariable:
            case EditorActionType.DecrementVariable:
            case EditorActionType.MultiplyVariable:
            case EditorActionType.DivideVariable:
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
            case EditorActionType.MousePosition:
            case EditorActionType.ClipboardGet:
            case EditorActionType.ClipboardSet:
            case EditorActionType.CopySelectionToVariable:
            case EditorActionType.ShellCommand:
            case EditorActionType.Screenshot:
            case EditorActionType.WindowCommand:
                break;
        }

        ApplyActionRandomDelay(events, action);
        return events;
    }

    internal static void ApplyActionRandomDelay(List<MacroEvent> events, EditorAction action)
    {
        if (action.Type is EditorActionType.Delay || !action.UseRandomDelay || events.Count is 0)
        {
            return;
        }

        var firstEvent = events[0];
        firstEvent.DelayMicroseconds = 0;
        firstEvent.HasRandomDelay = true;
        firstEvent.RandomDelayMinMs = action.RandomDelayMinMs;
        firstEvent.RandomDelayMaxMs = action.RandomDelayMaxMs;
        events[0] = firstEvent;
    }

    internal static bool UsesPositionCoordinates(EditorAction action)
    {
        return action.Type is EditorActionType.MouseMove || (action.Type is EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp && !action.UseCurrentPosition);
    }

    internal static bool TryGetTextInputControlKeyCode(char character, out int keyCode)
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

    internal static void AddKeyStroke(List<MacroEvent> events, int keyCode, ref bool isFirst, long initialDelayMicroseconds, bool needsShift = false, bool needsAltGr = false)
    {
        if (needsShift)
        {
            events.Add(new MacroEvent { Type = EventType.KeyPress, KeyCode = InputEventCode.KEY_LEFTSHIFT, DelayMicroseconds = 0, });
        }

        if (needsAltGr)
        {
            events.Add(new MacroEvent { Type = EventType.KeyPress, KeyCode = InputEventCode.KEY_RIGHTALT, DelayMicroseconds = 0, });
        }

        events.Add(new MacroEvent { Type = EventType.KeyPress, KeyCode = keyCode, DelayMicroseconds = isFirst ? initialDelayMicroseconds : MacroTiming.DefaultKeyPressDelayMicroseconds, });
        events.Add(new MacroEvent { Type = EventType.KeyRelease, KeyCode = keyCode, DelayMicroseconds = 0, });
        if (needsAltGr)
        {
            events.Add(new MacroEvent { Type = EventType.KeyRelease, KeyCode = InputEventCode.KEY_RIGHTALT, DelayMicroseconds = 0, });
        }

        if (needsShift)
        {
            events.Add(new MacroEvent { Type = EventType.KeyRelease, KeyCode = InputEventCode.KEY_LEFTSHIFT, DelayMicroseconds = 0, });
        }

        isFirst = false;
    }

    internal static MouseCoordinateMode? GetCoordinateMode(EditorAction action)
    {
        if (action.UseCurrentPosition || IsScrollButton(action.Button))
        {
            return null;
        }

        return action.IsAbsolute ? MouseCoordinateMode.Absolute : MouseCoordinateMode.Relative;
    }

    internal static MouseCoordinateSpace? GetCoordinateSpace(EditorAction action)
    {
        return GetCoordinateMode(action) switch
        {
            MouseCoordinateMode.Absolute => MouseCoordinateSpace.LogicalDesktop,
            MouseCoordinateMode.Relative => action.CoordinateSpace,
            null => null,
            _ => null,
        };
    }

    internal static MacroEvent CloneEvent(MacroEvent ev)
    {
        return ev;
    }

    internal EditorAction CreateKeyAction(EditorActionType type, int keyCode)
    {
        return new EditorAction
        {
            Type = type,
            KeyCode = keyCode,
            KeyName = _keyCodeMapper.GetKeyName(keyCode),
        };
    }

    internal EditorAction FromMacroEvent(MacroEvent ev)
    {
        var action = new EditorAction
        {
            DelayMicroseconds = ev.DelayMicroseconds,
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
                    action.CoordinateSpace = ResolveExplicitEditorCoordinateSpace(ev);
                }

                break;
            case EventType.Click:
                if (IsScrollButton(ev.Button))
                {
                    action.Type = ev.Button is MacroMouseButton.ScrollUp or MacroMouseButton.ScrollDown ? EditorActionType.ScrollVertical : EditorActionType.ScrollHorizontal;
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
                        action.CoordinateSpace = ResolveExplicitEditorCoordinateSpace(ev);
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
                    action.CoordinateSpace = ResolveExplicitEditorCoordinateSpace(ev);
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
                    action.CoordinateSpace = ResolveExplicitEditorCoordinateSpace(ev);
                }

                break;
            case EventType.KeyPress:
                action.Type = EditorActionType.KeyDown;
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

    internal static MouseCoordinateSpace ResolveExplicitEditorCoordinateSpace(MacroEvent ev)
    {
        return ev.CoordinateMode switch
        {
            MouseCoordinateMode.Absolute => MouseCoordinateSpace.LogicalDesktop,
            MouseCoordinateMode.Relative => ev.CoordinateSpace ?? MouseCoordinateSpace.RawDevice,
            null => MouseCoordinateSpace.LogicalDesktop,
            _ => MouseCoordinateSpace.LogicalDesktop,
        };
    }

    internal List<EditorAction> RestoreActionsFromEvents(MacroSequence sequence)
    {
        var actions = new List<EditorAction>();
        var events = sequence.Events;
        var useLegacyCurrentPositionInterpretation = MacroPositionSemantics.IsLegacyCurrentPositionMacro(sequence);
        var textInputBoundaries = CreateTextInputBoundaryLookup(sequence);
        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            if (textInputBoundaries.TryGetValue(i, out var textInputBoundary))
            {
                AppendDelayActions(actions, ev.DelayMicroseconds, ev.HasRandomDelay, ev.RandomDelayMinMs, ev.RandomDelayMaxMs);
                var textInputAction = new EditorAction
                {
                    Type = EditorActionType.TextInput,
                    Text = textInputBoundary.Text,
                };
                textInputAction.PreserveTextInputEvents(CopyBoundaryEventsWithoutLeadingDelay(events, textInputBoundary.StartEventIndex, textInputBoundary.EventCount));
                actions.Add(textInputAction);
                i += textInputBoundary.EventCount - 1;
                continue;
            }

            var action = FromMacroEvent(ev);
            // Set IsAbsolute from event-level mode, falling back to legacy sequence metadata.
            if (action.Type is EditorActionType.MouseMove or EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp)
            {
                if ((action.Type is EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp) && MacroPositionSemantics.UsesCurrentPosition(ev, useLegacyCurrentPositionInterpretation))
                {
                    action.UseCurrentPosition = true;
                    action.IsAbsolute = false;
                    action.CoordinateSpace = MouseCoordinateSpace.LogicalDesktop;
                    action.X = 0;
                    action.Y = 0;
                }
                else
                {
                    action.IsAbsolute = MacroPositionSemantics.ResolveCoordinateMode(ev, sequence.IsAbsoluteCoordinates) is MouseCoordinateMode.Absolute;
                    action.CoordinateSpace = MacroPositionSemantics.ResolveCoordinateSpace(ev, sequence.IsAbsoluteCoordinates) ?? MouseCoordinateSpace.LogicalDesktop;
                }
            }

            if (action.Type is EditorActionType.Delay)
            {
                if (action.DelayMicroseconds > 0 || action.UseRandomDelay)
                {
                    actions.Add(action);
                }

                continue;
            }

            AppendDelayActions(actions, action.DelayMicroseconds, action.UseRandomDelay, action.RandomDelayMinMs, action.RandomDelayMaxMs);
            action.DelayMicroseconds = 0;
            action.UseRandomDelay = false;
            action.RandomDelayMinMs = 0;
            action.RandomDelayMaxMs = 0;
            actions.Add(action);
        }

        // Add trailing delay as Delay action(s) if present.
        AppendDelayActions(actions, sequence.TrailingDelayMicroseconds, sequence.HasTrailingRandomDelay, sequence.TrailingDelayMinMs, sequence.TrailingDelayMaxMs);
        return actions;
    }

    internal Dictionary<int, TextInputBoundary> CreateTextInputBoundaryLookup(MacroSequence sequence)
    {
        if (sequence.TextInputBoundaries.Count is 0 || sequence.Events.Count is 0)
        {
            return new Dictionary<int, TextInputBoundary>();
        }

        var boundaries = sequence.TextInputBoundaries.OrderBy(boundary => boundary.StartEventIndex).ToList();
        var lookup = new Dictionary<int, TextInputBoundary>();
        var previousEndExclusive = 0;
        foreach (var boundary in boundaries)
        {
            if (boundary.StartEventIndex < previousEndExclusive || boundary.EventCount <= 0 || boundary.StartEventIndex < 0 || boundary.StartEventIndex + boundary.EventCount > sequence.Events.Count || !BoundaryMatchesTextInputEvents(sequence.Events, boundary))
            {
                return new Dictionary<int, TextInputBoundary>();
            }

            lookup.Add(boundary.StartEventIndex, boundary);
            previousEndExclusive = boundary.StartEventIndex + boundary.EventCount;
        }

        return lookup;
    }

    internal bool BoundaryMatchesTextInputEvents(IList<MacroEvent> events, TextInputBoundary boundary)
    {
        var expectedEvents = ToMacroEvents(new EditorAction { Type = EditorActionType.TextInput, Text = boundary.Text, });
        if (expectedEvents.Count != boundary.EventCount)
        {
            return false;
        }

        for (var offset = 0; offset < boundary.EventCount; offset++)
        {
            var actual = events[boundary.StartEventIndex + offset];
            var expected = expectedEvents[offset];
            if (actual.Type is not (EventType.KeyPress or EventType.KeyRelease) || actual.Type != expected.Type || actual.KeyCode != expected.KeyCode)
            {
                return false;
            }
        }

        return true;
    }

    internal static List<MacroEvent> CopyBoundaryEventsWithoutLeadingDelay(IList<MacroEvent> events, int startEventIndex, int eventCount)
    {
        var preserved = new List<MacroEvent>(eventCount);
        for (var offset = 0; offset < eventCount; offset++)
        {
            var ev = events[startEventIndex + offset];
            if (offset is 0)
            {
                ev.DelayMicroseconds = 0;
                ev.HasRandomDelay = false;
                ev.RandomDelayMinMs = 0;
                ev.RandomDelayMaxMs = 0;
            }

            preserved.Add(ev);
        }

        return preserved;
    }

    internal static void AppendDelayActions(List<EditorAction> actions, long fixedDelayMicroseconds, bool hasRandomDelay, int randomDelayMinMs, int randomDelayMaxMs)
    {
        if (fixedDelayMicroseconds > 0)
        {
            actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMicroseconds = fixedDelayMicroseconds, UseRandomDelay = false, });
        }

        if (hasRandomDelay)
        {
            actions.Add(new EditorAction { Type = EditorActionType.Delay, UseRandomDelay = true, RandomDelayMinMs = randomDelayMinMs, RandomDelayMaxMs = randomDelayMaxMs, });
        }
    }

    internal static bool IsScrollButton(MacroMouseButton button)
    {
        return button is MacroMouseButton.ScrollUp or MacroMouseButton.ScrollDown or MacroMouseButton.ScrollLeft or MacroMouseButton.ScrollRight;
    }
}
