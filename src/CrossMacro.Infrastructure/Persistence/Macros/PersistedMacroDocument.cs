
namespace CrossMacro.Infrastructure.Persistence.Macros;

/// <summary>
/// Persistence-owned snapshot of a macro. The text codec may carry legacy fields
/// without making them part of the runtime model's ownership contract.
/// </summary>
public class PersistedMacroDocument
{
    public const int CurrentSchemaVersion = 2;
    public const string CurrentFormat = "CrossMacroFormatV2";

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string Format => $"CrossMacroFormatV{SchemaVersion.ToString(CultureInfo.InvariantCulture)}";
    public Guid Id { get; init; }
    public string Name { get; init; } = MacroNameDefaults.UnnamedMacroName;
    public IReadOnlyList<PersistedMacroEvent> Events { get; init; } = [];
    public IReadOnlyList<string> ScriptSteps { get; init; } = [];
    public IReadOnlyList<TextInputBoundary> TextInputBoundaries { get; init; } = [];
    public IReadOnlyDictionary<string, string> Images { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public DateTime CreatedAt { get; init; }
    public long TotalDurationMs { get; init; }
    public DateTime RecordedAt { get; init; }
    public TimeSpan ActualDuration { get; init; }
    public int MouseMoveCount { get; init; }
    public int ClickCount { get; init; }
    public double EventsPerSecond { get; init; }
    public bool IsAbsoluteCoordinates { get; init; }
    public bool SkipInitialZeroZero { get; init; }
    public int TrailingDelayMs { get; init; }
    public bool HasTrailingRandomDelay { get; init; }
    public int TrailingDelayMinMs { get; init; }
    public int TrailingDelayMaxMs { get; init; }

    public static PersistedMacroDocument FromRuntime(MacroSequence macro)
    {
        ArgumentNullException.ThrowIfNull(macro);
        return new PersistedMacroDocument
        {
            Id = macro.Id,
            Name = macro.Name,
            Events = macro.Events is null ? new List<PersistedMacroEvent>() : macro.Events.Select(PersistedMacroEvent.FromRuntime).ToList(),
            ScriptSteps = macro.ScriptSteps is null ? new List<string>() : new List<string>(macro.ScriptSteps),
            TextInputBoundaries = macro.TextInputBoundaries is null
                ? new List<TextInputBoundary>()
                : new List<TextInputBoundary>(macro.TextInputBoundaries),
            Images = macro.Images is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(macro.Images, StringComparer.Ordinal),
            CreatedAt = macro.CreatedAt,
            TotalDurationMs = macro.TotalDurationMs,
            RecordedAt = macro.RecordedAt,
            ActualDuration = macro.ActualDuration,
            MouseMoveCount = macro.MouseMoveCount,
            ClickCount = macro.ClickCount,
            EventsPerSecond = macro.EventsPerSecond,
            IsAbsoluteCoordinates = macro.IsAbsoluteCoordinates,
            SkipInitialZeroZero = macro.SkipInitialZeroZero,
            TrailingDelayMs = macro.TrailingDelayMs,
            HasTrailingRandomDelay = macro.HasTrailingRandomDelay,
            TrailingDelayMinMs = macro.TrailingDelayMinMs,
            TrailingDelayMaxMs = macro.TrailingDelayMaxMs,
        };
    }

    public MacroSequence ToRuntime()
    {
        var sequence = new MacroSequence
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
            TrailingDelayMs = TrailingDelayMs,
            HasTrailingRandomDelay = HasTrailingRandomDelay,
            TrailingDelayMinMs = TrailingDelayMinMs,
            TrailingDelayMaxMs = TrailingDelayMaxMs,
        };

        sequence.ReplaceEvents(Events.Select(static ev => ev.ToRuntime()).ToList());
        sequence.ReplaceScriptSteps(ScriptSteps);
        sequence.ReplaceTextInputBoundaries(TextInputBoundaries);
        sequence.ReplaceImages(Images);
        return sequence;
    }
}
