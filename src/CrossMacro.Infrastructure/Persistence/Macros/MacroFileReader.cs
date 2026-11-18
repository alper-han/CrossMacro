using CrossMacro.Infrastructure.Services.Macros;
using static CrossMacro.Infrastructure.Persistence.Macros.MacroFileLimits;

namespace CrossMacro.Infrastructure.Persistence.Macros;

/// <summary>
/// Reads one macro file from a stream. An instance holds the state of a single read and must not be reused.
/// </summary>
internal sealed class MacroFileReader(MacroAssetValidator assetValidator, StreamReader reader)
{
    private enum Section
    {
        Header,
        Script,
        Events,
    }

    private readonly MacroAssetValidator _assetValidator = assetValidator;
    private readonly BoundedLineReader _lineReader = new(reader, MaxMacroLineChars);
    private readonly MacroSequence _macro = new();
    private Section _section = Section.Header;
    private StringBuilder? _pendingScriptStep;
    private long _totalEncodedImageChars;
    private int _lineNumber;
    private int _scriptStepCount;
    private long _pendingDelayMicroseconds;
    private bool _pendingHasRandomDelay;
    private int _pendingRandomDelayMinMs;
    private int _pendingRandomDelayMaxMs;

    internal async Task<MacroSequence> ReadAsync()
    {
        while (await _lineReader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            _lineNumber++;
            if (_lineNumber > MaxMacroFileLines)
            {
                throw new InvalidDataException($"Macro file exceeds the maximum of {MaxMacroFileLines} lines.");
            }

            await ProcessLineAsync(line).ConfigureAwait(false);
        }

        CommitPendingScriptStep();
        RemoveInvalidActionComments(_macro);
        MarkLegacyCurrentPositionEvents(_macro);

        _macro.CalculateDuration();
        MacroStatistics.Apply(_macro);

        MacroAssetValidator.ValidateReferences(_macro.ScriptSteps, _macro.Images, "Loaded macro");
        return _macro;
    }

    private async Task ProcessLineAsync(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        if (string.Equals(trimmed, ScriptSectionHeader, StringComparison.Ordinal))
        {
            CommitPendingScriptStep();
            _section = Section.Script;
            return;
        }

        if (string.Equals(trimmed, EventsSectionHeader, StringComparison.Ordinal))
        {
            CommitPendingScriptStep();
            _section = Section.Events;
            return;
        }

        if (_section is Section.Script)
        {
            ProcessScriptLine(line, trimmed);
            return;
        }

        if (trimmed.StartsWith('#'))
        {
            if (_section is Section.Header)
            {
                await ProcessHeaderLineAsync(line).ConfigureAwait(false);
            }

            return;
        }

        ProcessEventLine(line);
    }

    private void ProcessScriptLine(string line, string trimmed)
    {
        if (trimmed.StartsWith('#'))
        {
            return;
        }

        if (line.StartsWith(ScriptContinuationPrefix, StringComparison.Ordinal))
        {
            if (_pendingScriptStep is null)
            {
                Log.Warning("Ignoring orphan script continuation line: {Line}", line);
                return;
            }

            _ = _pendingScriptStep.Append('\n').Append(line[ScriptContinuationPrefix.Length..]);
            return;
        }

        CommitPendingScriptStep();
        _pendingScriptStep = new StringBuilder(line);
    }

    private void CommitPendingScriptStep()
    {
        var scriptStep = _pendingScriptStep?.ToString();
        if (!string.IsNullOrWhiteSpace(scriptStep))
        {
            if (++_scriptStepCount > MaxMacroScriptSteps)
            {
                throw new InvalidDataException($"Macro script exceeds the maximum of {MaxMacroScriptSteps} steps.");
            }

            _macro.ScriptSteps.Add(scriptStep);
        }

        _pendingScriptStep = null;
    }

    private async Task ProcessHeaderLineAsync(string line)
    {
        if (line.StartsWith(ImageHeader, StringComparison.Ordinal))
        {
            _totalEncodedImageChars = await _assetValidator.AddImageMetadataAsync(_macro, line, _totalEncodedImageChars).ConfigureAwait(false);
            return;
        }

        MacroHeaderParser.Apply(_macro, line);
    }

    private void ProcessEventLine(string line)
    {
        var parts = line.Split(',');
        var type = parts[0].ToUpperInvariant();

        if (TryApplyWait(parts, type))
        {
            return;
        }

        MacroEvent ev;
        try
        {
            if (!MacroEventLineParser.TryParse(parts, type, out ev))
            {
                Log.Warning("Ignoring unsupported or malformed event line: {Line}", line);
                ResetPendingEventTiming();
                return;
            }
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            Log.Warning(ex, "Error parsing line: {Line}", line);
            ResetPendingEventTiming();
            return;
        }

        if (_macro.Events.Count >= MaxMacroEvents)
        {
            throw new InvalidDataException($"Macro events exceed the maximum of {MaxMacroEvents} events at line {_lineNumber.ToString(CultureInfo.InvariantCulture)}.");
        }

        ev.DelayMicroseconds = _pendingDelayMicroseconds;
        ev.HasRandomDelay = _pendingHasRandomDelay;
        ev.RandomDelayMinMs = _pendingRandomDelayMinMs;
        ev.RandomDelayMaxMs = _pendingRandomDelayMaxMs;
        if (_macro.Events.Count > 0)
        {
            var randomMinMicroseconds = ev.HasRandomDelay ? (long)ev.RandomDelayMinMs * MacroTiming.MicrosecondsPerMillisecond : 0;
            ev.TimestampMicroseconds = AddTiming(
                AddTiming(_macro.Events[^1].TimestampMicroseconds, ev.DelayMicroseconds, _lineNumber),
                randomMinMicroseconds,
                _lineNumber);
        }

        _macro.Events.Add(ev);
        ResetPendingEventTiming();
    }

    /// <summary>Accumulates <c>W</c>, <c>WU</c> and <c>WR</c> lines into the delay of the next event.</summary>
    private bool TryApplyWait(string[] parts, string type)
    {
        if ((type is "W" or "WAIT") && parts.Length >= 2)
        {
            if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int delay))
            {
                _pendingDelayMicroseconds = AddTiming(
                    _pendingDelayMicroseconds, (long)delay * MacroTiming.MicrosecondsPerMillisecond, _lineNumber);
            }

            return true;
        }

        if ((type is "WU" or "WAITUS" or "WAITMICROSECONDS") && parts.Length >= 2)
        {
            if (long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long delayMicroseconds)
                && delayMicroseconds >= 0)
            {
                _pendingDelayMicroseconds = AddTiming(_pendingDelayMicroseconds, delayMicroseconds, _lineNumber);
            }

            return true;
        }

        if ((type is "WR" or "WAITRANDOM") && parts.Length >= 3)
        {
            if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int randomMinDelay) && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int randomMaxDelay))
            {
                _pendingHasRandomDelay = true;
                _pendingRandomDelayMinMs += randomMinDelay;
                _pendingRandomDelayMaxMs += randomMaxDelay;
            }

            return true;
        }

        return false;
    }

    private void ResetPendingEventTiming()
    {
        _pendingDelayMicroseconds = 0;
        _pendingHasRandomDelay = false;
        _pendingRandomDelayMinMs = 0;
        _pendingRandomDelayMaxMs = 0;
    }

    private static long AddTiming(long current, long addend, int lineNumber)
    {
        try
        {
            return checked(current + addend);
        }
        catch (OverflowException ex)
        {
            throw new InvalidDataException($"Macro timing overflows at line {lineNumber.ToString(CultureInfo.InvariantCulture)}.", ex);
        }
    }

    private static void RemoveInvalidActionComments(MacroSequence macro)
    {
        for (var index = macro.ActionComments.Count - 1; index >= 0; index--)
        {
            var boundary = macro.ActionComments[index];
            var eventRangeValid = boundary.EventStartIndex >= 0
                && boundary.EventCount >= 0
                && boundary.EventStartIndex <= macro.Events.Count
                && boundary.EventCount <= macro.Events.Count - boundary.EventStartIndex;
            var scriptRangeValid = boundary.ScriptStepStartIndex >= 0
                && boundary.ScriptStepCount >= 0
                && boundary.ScriptStepStartIndex <= macro.ScriptSteps.Count
                && boundary.ScriptStepCount <= macro.ScriptSteps.Count - boundary.ScriptStepStartIndex;
            if (!eventRangeValid || !scriptRangeValid || string.IsNullOrEmpty(boundary.Comment) || boundary.Comment.Length > MaxActionCommentChars)
            {
                macro.ActionComments.RemoveAt(index);
            }
        }
    }

    private static void MarkLegacyCurrentPositionEvents(MacroSequence macro)
    {
        if (macro.IsAbsoluteCoordinates
            || !macro.SkipInitialZeroZero
            || macro.Events.Any(ev => ev.UseCurrentPosition))
        {
            return;
        }

        var markedAny = false;

        for (int index = 0; index < macro.Events.Count; index++)
        {
            var ev = macro.Events[index];

            if (ev.Type is EventType.MouseMove)
            {
                if (ev.X is not 0 || ev.Y is not 0)
                {
                    break;
                }

                continue;
            }

            if (!MacroPositionSemantics.IsNonScrollMouseButtonEvent(ev))
            {
                continue;
            }

            if (MacroPositionSemantics.HasExplicitCoordinateMode(ev))
            {
                break;
            }

            if (ev.X is not 0 || ev.Y is not 0)
            {
                if (!markedAny)
                {
                    return;
                }

                break;
            }

            ev.UseCurrentPosition = true;
            macro.Events[index] = ev;
            markedAny = true;
        }
    }
}
