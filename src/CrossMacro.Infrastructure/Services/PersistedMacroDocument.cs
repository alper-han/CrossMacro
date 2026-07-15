
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
