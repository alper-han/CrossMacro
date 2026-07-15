using CrossMacro.Core.Models;
using Canonical = CrossMacro.Infrastructure.Persistence.Macros;

namespace CrossMacro.Infrastructure.Services;

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
