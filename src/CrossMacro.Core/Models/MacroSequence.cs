
namespace CrossMacro.Core.Models;

/// <summary>
/// Represents a complete macro sequence
/// </summary>
public class MacroSequence
{
    /// <summary>
    /// Unique identifier for this macro
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Name of the macro
    /// </summary>
    public string Name { get; set; } = MacroNameDefaults.UnnamedMacroName;

    /// <summary>
    /// List of events in the macro
    /// </summary>
    public IList<MacroEvent> Events { get; } = new List<MacroEvent>(10000);

    /// <summary>
    /// Optional source script steps that produced this macro.
    /// Used by the editor to restore structured script actions on reload.
    /// </summary>
    public IList<string> ScriptSteps { get; } = new List<string>();

    /// <summary>
    /// Optional editor metadata that preserves separate TextInput actions after they are expanded to key events.
    /// </summary>
    public IList<TextInputBoundary> TextInputBoundaries { get; } = new List<TextInputBoundary>();

    /// <summary>
    /// Named image assets stored as Base64 PNG strings.
    /// </summary>
    public IDictionary<string, string> Images { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public void ReplaceEvents(IEnumerable<MacroEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var replacement = events.ToList();
        Events.Clear();
        foreach (var macroEvent in replacement)
        {
            Events.Add(macroEvent);
        }
    }

    public void ReplaceScriptSteps(IEnumerable<string> scriptSteps)
    {
        ArgumentNullException.ThrowIfNull(scriptSteps);
        var replacement = scriptSteps.ToList();
        ScriptSteps.Clear();
        foreach (var scriptStep in replacement)
        {
            ScriptSteps.Add(scriptStep);
        }
    }

    public void ReplaceTextInputBoundaries(IEnumerable<TextInputBoundary> textInputBoundaries)
    {
        ArgumentNullException.ThrowIfNull(textInputBoundaries);
        var replacement = textInputBoundaries.ToList();
        TextInputBoundaries.Clear();
        foreach (var boundary in replacement)
        {
            TextInputBoundaries.Add(boundary);
        }
    }

    public void ReplaceImages(IEnumerable<KeyValuePair<string, string>> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        var replacement = images.ToList();
        Images.Clear();
        foreach (var image in replacement)
        {
            Images.Add(image.Key, image.Value);
        }
    }

    /// <summary>
    /// When the macro was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total duration of the macro in milliseconds
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// Number of events in the macro
    /// </summary>
    public int EventCount => Events.Count;

    // Statistics metadata
    /// <summary>
    /// When the macro was recorded
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Actual recording duration (wall clock time)
    /// </summary>
    public TimeSpan ActualDuration { get; set; }

    /// <summary>
    /// Number of mouse move events
    /// </summary>
    public int MouseMoveCount { get; set; }

    /// <summary>
    /// Number of click events
    /// </summary>
    public int ClickCount { get; set; }

    /// <summary>
    /// Events recorded per second
    /// </summary>
    public double EventsPerSecond { get; set; }

    /// <summary>
    /// Whether the macro contains absolute coordinates (true) or relative deltas (false)
    /// </summary>
    public bool IsAbsoluteCoordinates { get; set; }

    /// <summary>
    /// Whether Corner Reset was skipped during recording.
    /// If false and IsAbsoluteCoordinates is false, playback should do Corner Reset to 0,0 first.
    /// </summary>
    public bool SkipInitialZeroZero { get; set; }

    private long _trailingDelayMicroseconds;

    /// <summary>Precise delay after the final event, in microseconds.</summary>
    public long TrailingDelayMicroseconds
    {
        get => _trailingDelayMicroseconds;
        set => _trailingDelayMicroseconds = Math.Max(0, value);
    }

    /// <summary>Legacy millisecond projection of <see cref="TrailingDelayMicroseconds"/>.</summary>
    public int TrailingDelayMs
    {
        get => MacroTiming.ToLegacyMilliseconds(_trailingDelayMicroseconds);
        set => _trailingDelayMicroseconds = Math.Max(0, (long)value * MacroTiming.MicrosecondsPerMillisecond);
    }

    /// <summary>
    /// Whether trailing delay includes a randomized component.
    /// </summary>
    public bool HasTrailingRandomDelay { get; set; }

    /// <summary>
    /// Minimum randomized trailing delay in milliseconds.
    /// </summary>
    public int TrailingDelayMinMs { get; set; }

    /// <summary>
    /// Maximum randomized trailing delay in milliseconds.
    /// </summary>
    public int TrailingDelayMaxMs { get; set; }

    /// <summary>
    /// Validates the macro sequence
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        if ((Events is null || Events.Count is 0) && !HasScriptSteps())
        {
            return false;
        }

        if (Events is not null && !Events.All(IsEventTimingValid))
        {
            return false;
        }

        return !HasTrailingRandomDelay
            || (TrailingDelayMinMs >= 0 && TrailingDelayMaxMs >= TrailingDelayMinMs);
    }

    private static bool IsEventTimingValid(MacroEvent ev)
    {
        if (ev.TimestampMicroseconds < 0 || ev.DelayMicroseconds < 0)
        {
            return false;
        }

        if (!ev.HasRandomDelay)
        {
            return true;
        }

        return ev.RandomDelayMinMs >= 0 && ev.RandomDelayMaxMs >= ev.RandomDelayMinMs;
    }

    private bool HasScriptSteps()
    {
        return ScriptSteps is not null && ScriptSteps.Any(static step => !string.IsNullOrWhiteSpace(step));
    }

    /// <summary>
    /// Calculates total duration from events
    /// </summary>
    public void CalculateDuration()
    {
        if (Events.Count is 0)
        {
            TotalDurationMs = 0;
            return;
        }

        TotalDurationMs = Events[^1].TimestampMicroseconds / MacroTiming.MicrosecondsPerMillisecond;
    }

    /// <summary>
    /// Creates a detached copy of the macro sequence.
    /// </summary>
    public MacroSequence Clone()
    {
        var clone = new MacroSequence
        {
            Id = Id,
            Name = Name,
            CreatedAt = CreatedAt,
            TotalDurationMs = TotalDurationMs,
            RecordedAt = RecordedAt,
            ActualDuration = ActualDuration,
            MouseMoveCount = MouseMoveCount,
            ClickCount = ClickCount,
            EventsPerSecond = EventsPerSecond,
            IsAbsoluteCoordinates = IsAbsoluteCoordinates,
            SkipInitialZeroZero = SkipInitialZeroZero,
            TrailingDelayMicroseconds = TrailingDelayMicroseconds,
            HasTrailingRandomDelay = HasTrailingRandomDelay,
            TrailingDelayMinMs = TrailingDelayMinMs,
            TrailingDelayMaxMs = TrailingDelayMaxMs,
        };

        clone.ReplaceEvents(Events);
        clone.ReplaceScriptSteps(ScriptSteps);
        clone.ReplaceTextInputBoundaries(TextInputBoundaries);
        clone.ReplaceImages(Images);
        return clone;
    }
}
