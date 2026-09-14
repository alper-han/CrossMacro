namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>
/// Converts between EditorAction and MacroEvent/MacroSequence.
/// Handles bidirectional conversion while preserving .macro format compatibility.
/// </summary>
public class EditorActionConverter : IEditorActionConverter
{
    private readonly EditorEventProjection _events;
    private readonly EditorScriptReader _reader;
    public IReadOnlyList<MacroEvent> ToMacroEvents(EditorAction action) => _events.ToMacroEvents(action);
    public EditorAction FromMacroEvent(MacroEvent ev) => _events.FromMacroEvent(ev);
    internal static string BuildWindowStep(EditorAction action) => EditorScriptWriter.BuildWindowStep(action);

    private readonly RunScriptCompiler _runScriptCompiler;
    public EditorActionConverter(IKeyCodeMapper keyCodeMapper)
    {
        ArgumentNullException.ThrowIfNull(keyCodeMapper);
        _events = new EditorEventProjection(keyCodeMapper);
        _reader = new EditorScriptReader(keyCodeMapper, _events);
        _runScriptCompiler = new RunScriptCompiler(keyCodeMapper);
    }

    /// <summary>
    /// Restores a runtime sequence into the editor projection boundary.
    /// </summary>
    public EditorMacroProjection FromMacroSequenceProjection(MacroSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        return new EditorMacroProjection(FromMacroSequence(sequence), sequence.Name, sequence.IsAbsoluteCoordinates, sequence.SkipInitialZeroZero);
    }

    /// <summary>
    /// Converts the editor projection while retaining the existing conversion
    /// implementation as the compatibility facade.
    /// </summary>
    public MacroSequence ToMacroSequence(EditorMacroProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return ToMacroSequence(projection.Actions, projection.Name, projection.IsAbsoluteCoordinates, projection.SkipInitialZeroZero);
    }

    /// <inheritdoc/>
    public MacroSequence ToMacroSequence(IEnumerable<EditorAction> actions, string name, bool isAbsolute, bool skipInitialZeroZero = false)
    {
        var actionList = actions.ToList();
        var hasFlowControlScriptActions = actionList.Exists(action => EditorActionScriptClassifier.IsScriptFlowControlAction(action.Type));
        var hasStateScriptActions = actionList.Exists(action => EditorActionScriptClassifier.IsScriptStateAction(action.Type));
        var hasOpaqueScriptActions = actionList.Exists(action => EditorActionScriptClassifier.IsOpaqueScriptAction(action.Type));
        var hasRuntimeEventActions = actionList.Exists(action => EditorActionScriptClassifier.IsRuntimeEventAction(action.Type));
        var hasScriptCoordinateActions = actionList.Exists(action => EditorEventProjection.UsesPositionCoordinates(action) && !action.TryGetLiteralCoordinates(out _, out _));
        if (hasFlowControlScriptActions || hasOpaqueScriptActions || hasScriptCoordinateActions || (hasStateScriptActions && !hasRuntimeEventActions))
        {
            return CompileScriptBackedSequence(actionList, name, skipInitialZeroZero);
        }

        var sequence = new MacroSequence
        {
            Name = name,
            IsAbsoluteCoordinates = isAbsolute,
            SkipInitialZeroZero = skipInitialZeroZero,
            CreatedAt = DateTime.UtcNow,
        };
        long timestampMicroseconds = 0;
        long pendingDelayMicroseconds = 0;
        bool hasPendingRandomDelay = false;
        int pendingRandomDelayMinMs = 0;
        int pendingRandomDelayMaxMs = 0;
        foreach (var action in actionList)
        {
            var events = _events.ToMacroEvents(action);
            var actionStartEventIndex = sequence.Events.Count;
            var actionEventCount = 0;
            foreach (var ev in events)
            {
                // Skip None type events but accumulate their delay
                if (ev.Type is EventType.None)
                {
                    pendingDelayMicroseconds += ev.DelayMicroseconds;
                    if (ev.HasRandomDelay)
                    {
                        hasPendingRandomDelay = true;
                        pendingRandomDelayMinMs += ev.RandomDelayMinMs;
                        pendingRandomDelayMaxMs += ev.RandomDelayMaxMs;
                    }

                    continue;
                }

                var eventToAdd = ev;
                eventToAdd.DelayMicroseconds += pendingDelayMicroseconds;
                if (hasPendingRandomDelay)
                {
                    eventToAdd.HasRandomDelay = true;
                    eventToAdd.RandomDelayMinMs += pendingRandomDelayMinMs;
                    eventToAdd.RandomDelayMaxMs += pendingRandomDelayMaxMs;
                }

                eventToAdd.TimestampMicroseconds = timestampMicroseconds;
                timestampMicroseconds += eventToAdd.DelayMicroseconds;
                if (eventToAdd.HasRandomDelay)
                {
                    timestampMicroseconds += (long)eventToAdd.RandomDelayMinMs * MacroTiming.MicrosecondsPerMillisecond;
                }

                pendingDelayMicroseconds = 0;
                hasPendingRandomDelay = false;
                pendingRandomDelayMinMs = 0;
                pendingRandomDelayMaxMs = 0;
                sequence.Events.Add(eventToAdd);
                actionEventCount++;
            }

            if (action.Type is EditorActionType.TextInput && actionEventCount > 0)
            {
                sequence.TextInputBoundaries.Add(new TextInputBoundary(actionStartEventIndex, actionEventCount, action.Text));
            }
        }

        // Preserve trailing delay for looped macros
        if (pendingDelayMicroseconds > 0 || hasPendingRandomDelay)
        {
            sequence.TrailingDelayMicroseconds = pendingDelayMicroseconds;
            sequence.HasTrailingRandomDelay = hasPendingRandomDelay;
            sequence.TrailingDelayMinMs = pendingRandomDelayMinMs;
            sequence.TrailingDelayMaxMs = pendingRandomDelayMaxMs;
        }

        sequence.CalculateDuration();
        sequence.MouseMoveCount = sequence.Events.Count(e => e.Type is EventType.MouseMove);
        sequence.ClickCount = sequence.Events.Count(e => e.Type is not EventType.MouseMove);
        if (hasStateScriptActions)
        {
            var scriptSteps = EditorScriptWriter.BuildScriptSteps(actionList);
            sequence.ReplaceScriptSteps(scriptSteps.Select(step => step.Step).Where(step => !string.IsNullOrWhiteSpace(step)).ToList());
            ClearTrailingDelayWhenScriptExecutes(sequence, scriptSteps);
        }

        return sequence;
    }

    private MacroSequence CompileScriptBackedSequence(IReadOnlyList<EditorAction> actions, string name, bool skipInitialZeroZero)
    {
        var scriptSteps = EditorScriptWriter.BuildScriptSteps(actions);
        var compileResult = _runScriptCompiler.Compile(scriptSteps);
        if (!compileResult.Success || compileResult.Sequence is null)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(compileResult.ErrorMessage) ? "Script compilation failed." : compileResult.ErrorMessage);
        }

        var sequence = compileResult.Sequence;
        sequence.Name = name;
        sequence.CreatedAt = DateTime.UtcNow;
        sequence.SkipInitialZeroZero = skipInitialZeroZero;
        sequence.ReplaceScriptSteps(scriptSteps.Select(step => step.Step).Where(step => !string.IsNullOrWhiteSpace(step)).ToList());
        ClearTrailingDelayWhenScriptExecutes(sequence, scriptSteps);
        if (sequence.Events.Count > 0 && (compileResult.InitialDelayMicroseconds > 0 || compileResult.InitialHasRandomDelay))
        {
            var firstEvent = sequence.Events[0];
            firstEvent.DelayMicroseconds += compileResult.InitialDelayMicroseconds;
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

    private static void ClearTrailingDelayWhenScriptExecutes(MacroSequence sequence, IReadOnlyList<RunScriptStep> scriptSteps)
    {
        if (!scriptSteps.Any(step => RunScriptRuntimeStepClassifier.IsRuntimeStep(step.Step)))
        {
            return;
        }

        sequence.TrailingDelayMicroseconds = 0;
        sequence.HasTrailingRandomDelay = false;
        sequence.TrailingDelayMinMs = 0;
        sequence.TrailingDelayMaxMs = 0;
    }

    private static void RecalculateTimestamps(MacroSequence sequence)
    {
        long timestampMicroseconds = 0;
        for (var i = 0; i < sequence.Events.Count; i++)
        {
            var ev = sequence.Events[i];
            ev.TimestampMicroseconds = timestampMicroseconds;
            timestampMicroseconds += ev.DelayMicroseconds;
            if (ev.HasRandomDelay)
            {
                timestampMicroseconds += (long)ev.RandomDelayMinMs * MacroTiming.MicrosecondsPerMillisecond;
            }

            sequence.Events[i] = ev;
        }
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
        if (_reader.TryRestoreActionsFromScriptSteps(sequence.ScriptSteps, out var scriptActions, out var warnings))
        {
            return new EditorActionRestoreResult(scriptActions, warnings, restoredFromScriptSteps: true);
        }

        var eventActions = _events.RestoreActionsFromEvents(sequence);
        return new EditorActionRestoreResult(eventActions, [], restoredFromScriptSteps: false);
    }
}
