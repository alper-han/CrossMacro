using System;
using System.Collections.Generic;
using CrossMacro.Core.Models;

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
    public string Format => $"CrossMacroFormatV{SchemaVersion}";
    public Guid Id { get; init; }
    public string Name { get; init; } = MacroNameDefaults.UnnamedMacroName;
    public List<PersistedMacroEvent> Events { get; init; } = new();
    public List<string> ScriptSteps { get; init; } = new();
    public List<TextInputBoundary> TextInputBoundaries { get; init; } = new();
    public Dictionary<string, string> Images { get; init; } = new(StringComparer.Ordinal);
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
            Events = macro.Events is null ? new List<PersistedMacroEvent>() : macro.Events.ConvertAll(PersistedMacroEvent.FromRuntime),
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
        return new MacroSequence
        {
            Id = Id,
            Name = Name,
            Events = Events.ConvertAll(static ev => ev.ToRuntime()),
            ScriptSteps = new List<string>(ScriptSteps),
            TextInputBoundaries = new List<TextInputBoundary>(TextInputBoundaries),
            Images = new Dictionary<string, string>(Images, StringComparer.Ordinal),
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
    }
}

public class PersistedMacroEvent
{
    public EventType Type { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public MouseButton Button { get; init; }
    public long Timestamp { get; init; }
    public int DelayMs { get; init; }
    public bool HasRandomDelay { get; init; }
    public int RandomDelayMinMs { get; init; }
    public int RandomDelayMaxMs { get; init; }
    public int KeyCode { get; init; }
    public MouseCoordinateMode? CoordinateMode { get; init; }
    public bool UseCurrentPosition { get; init; }

    public static PersistedMacroEvent FromRuntime(MacroEvent ev) => new()
    {
        Type = ev.Type,
        X = ev.X,
        Y = ev.Y,
        Button = ev.Button,
        Timestamp = ev.Timestamp,
        DelayMs = ev.DelayMs,
        HasRandomDelay = ev.HasRandomDelay,
        RandomDelayMinMs = ev.RandomDelayMinMs,
        RandomDelayMaxMs = ev.RandomDelayMaxMs,
        KeyCode = ev.KeyCode,
        CoordinateMode = ev.CoordinateMode,
        UseCurrentPosition = ev.UseCurrentPosition,
    };

    public MacroEvent ToRuntime() => new()
    {
        Type = Type,
        X = X,
        Y = Y,
        Button = Button,
        Timestamp = Timestamp,
        DelayMs = DelayMs,
        HasRandomDelay = HasRandomDelay,
        RandomDelayMinMs = RandomDelayMinMs,
        RandomDelayMaxMs = RandomDelayMaxMs,
        KeyCode = KeyCode,
        CoordinateMode = CoordinateMode,
        UseCurrentPosition = UseCurrentPosition,
    };
}

public static class PersistedMacroCodec
{
    public static PersistedMacroDocument Encode(MacroSequence macro) => PersistedMacroDocument.FromRuntime(macro);

    public static MacroSequence Decode(PersistedMacroDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion > PersistedMacroDocument.CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported macro schema version {document.SchemaVersion}.");
        }

        return document.ToRuntime();
    }
}
