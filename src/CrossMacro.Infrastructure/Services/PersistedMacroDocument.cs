using System;
using CrossMacro.Core.Models;
using Canonical = CrossMacro.Infrastructure.Persistence.Macros;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Compatibility forwarding type. New persistence consumers use the explicit macro persistence namespace.
/// </summary>
public sealed class PersistedMacroDocument : Canonical.PersistedMacroDocument
{
    public new static PersistedMacroDocument FromRuntime(MacroSequence macro)
    {
        var document = Canonical.PersistedMacroDocument.FromRuntime(macro);
        return new PersistedMacroDocument
        {
            SchemaVersion = document.SchemaVersion,
            Id = document.Id,
            Name = document.Name,
            Events = document.Events,
            ScriptSteps = document.ScriptSteps,
            TextInputBoundaries = document.TextInputBoundaries,
            Images = document.Images,
            CreatedAt = document.CreatedAt,
            TotalDurationMs = document.TotalDurationMs,
            RecordedAt = document.RecordedAt,
            ActualDuration = document.ActualDuration,
            MouseMoveCount = document.MouseMoveCount,
            ClickCount = document.ClickCount,
            EventsPerSecond = document.EventsPerSecond,
            IsAbsoluteCoordinates = document.IsAbsoluteCoordinates,
            SkipInitialZeroZero = document.SkipInitialZeroZero,
            TrailingDelayMs = document.TrailingDelayMs,
            HasTrailingRandomDelay = document.HasTrailingRandomDelay,
            TrailingDelayMinMs = document.TrailingDelayMinMs,
            TrailingDelayMaxMs = document.TrailingDelayMaxMs,
        };
    }
}

public sealed class PersistedMacroEvent : Canonical.PersistedMacroEvent
{
    public new static PersistedMacroEvent FromRuntime(MacroEvent ev)
    {
        var result = Canonical.PersistedMacroEvent.FromRuntime(ev);
        return new PersistedMacroEvent
        {
            Type = result.Type,
            X = result.X,
            Y = result.Y,
            Button = result.Button,
            Timestamp = result.Timestamp,
            DelayMs = result.DelayMs,
            HasRandomDelay = result.HasRandomDelay,
            RandomDelayMinMs = result.RandomDelayMinMs,
            RandomDelayMaxMs = result.RandomDelayMaxMs,
            KeyCode = result.KeyCode,
            CoordinateMode = result.CoordinateMode,
            UseCurrentPosition = result.UseCurrentPosition,
        };
    }
}

public static class PersistedMacroCodec
{
    public static PersistedMacroDocument Encode(MacroSequence macro) => PersistedMacroDocument.FromRuntime(macro);

    public static MacroSequence Decode(PersistedMacroDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Canonical.PersistedMacroCodec.Decode(document);
    }
}
