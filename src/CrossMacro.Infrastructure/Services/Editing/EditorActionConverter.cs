using CrossMacro.Editing.Services;
using CrossMacro.Infrastructure.Services.Macros;

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
        var hasScriptCoordinateActions = actionList.Exists(action => EditorActionCoordinateRules.HasPositionCoordinates(action) && !action.TryGetLiteralCoordinates(out _, out _));
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
        var assembler = new EditorEventSequenceAssembler(sequence);
        foreach (var action in actionList)
        {
            assembler.Append(action, _events.ToMacroEvents(action));
        }

        // Preserve trailing delay for looped macros
        assembler.ApplyTrailingDelay();

        sequence.CalculateDuration();
        MacroStatistics.Apply(sequence);
        if (hasStateScriptActions)
        {
            var scriptSteps = EditorScriptWriter.BuildScriptSteps(actionList);
            var projectedSteps = scriptSteps.Where(step => !string.IsNullOrWhiteSpace(step.Step)).ToList();
            sequence.ReplaceScriptSteps(projectedSteps.Select(step => step.Step).ToList());
            AddScriptCommentRanges(sequence, actionList, projectedSteps);
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
        var projectedSteps = scriptSteps.Where(step => !string.IsNullOrWhiteSpace(step.Step)).ToList();
        sequence.ReplaceScriptSteps(projectedSteps.Select(step => step.Step).ToList());
        AddScriptCommentRanges(sequence, actions, projectedSteps);
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
        MacroStatistics.Apply(sequence);
        return sequence;
    }

    private static void AddScriptCommentRanges(MacroSequence sequence, IReadOnlyList<EditorAction> actions, IReadOnlyList<RunScriptStep> scriptSteps)
    {
        for (var actionIndex = 0; actionIndex < actions.Count; actionIndex++)
        {
            var action = actions[actionIndex];
            if (string.IsNullOrEmpty(action.Comment))
            {
                continue;
            }

            var sourceIndex = actionIndex + 1;
            var matchingIndices = scriptSteps.Select((step, index) => (step, index)).Where(item => item.step.SourceIndex == sourceIndex).Select(item => item.index).ToList();
            var scriptStart = matchingIndices.Count is 0 ? 0 : matchingIndices[0];
            var scriptCount = matchingIndices.Count;
            sequence.ActionComments.Add(new ActionCommentBoundary(0, 0, scriptStart, scriptCount, action.Comment));
        }
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
            ApplyScriptComments(sequence, scriptActions);
            return new EditorActionRestoreResult(scriptActions, warnings, restoredFromScriptSteps: true);
        }

        var eventActions = _events.RestoreActionsFromEvents(sequence);
        ApplyEventComments(sequence, eventActions);
        return new EditorActionRestoreResult(eventActions, [], restoredFromScriptSteps: false);
    }

    private static void ApplyScriptComments(MacroSequence sequence, List<EditorAction> actions)
    {
        var restoredSteps = EditorScriptWriter.BuildScriptSteps(actions);
        var stepIndex = 0;
        for (var actionIndex = 0; actionIndex < actions.Count; actionIndex++)
        {
            var actionStepCount = restoredSteps.Count(step => step.SourceIndex == actionIndex + 1);
            foreach (var boundary in sequence.ActionComments)
            {
                if (boundary.ScriptStepCount > 0 && stepIndex < boundary.ScriptStepStartIndex + boundary.ScriptStepCount
                    && stepIndex + actionStepCount > boundary.ScriptStepStartIndex)
                {
                    actions[actionIndex].Comment = boundary.Comment;
                    break;
                }
            }

            stepIndex += actionStepCount;
        }
    }

    private void ApplyEventComments(MacroSequence sequence, List<EditorAction> actions)
    {
        var eventRanges = new List<(int Start, int Count, EditorAction Action)>();
        var eventIndex = 0;
        foreach (var action in actions)
        {
            var count = _events.ToMacroEvents(action).Count(ev => ev.Type is not EventType.None);
            eventRanges.Add((eventIndex, count, action));
            eventIndex += count;
        }

        foreach (var boundary in sequence.ActionComments)
        {
            if (boundary.EventStartIndex < 0 || boundary.EventCount < 0
                || boundary.EventStartIndex + boundary.EventCount > sequence.Events.Count)
            {
                continue;
            }

            var matchingRange = boundary.EventCount is 0
                ? eventRanges.FirstOrDefault(range => range.Start == boundary.EventStartIndex && range.Action.Type is EditorActionType.Delay)
                : eventRanges.FirstOrDefault(range => range.Count > 0
                    && range.Start < boundary.EventStartIndex + boundary.EventCount
                    && range.Start + range.Count > boundary.EventStartIndex);
            if (matchingRange.Action is { } target)
            {
                target.Comment = string.IsNullOrEmpty(target.Comment)
                    ? boundary.Comment
                    : string.Concat(target.Comment, Environment.NewLine, boundary.Comment);
            }
        }
    }
}
