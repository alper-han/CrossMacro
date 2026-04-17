using System.Collections.Generic;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.Models;

internal sealed record PlaybackExecutionPlan(
    LoadedMacroPlaybackMode Mode,
    MacroSequence? ActiveMacro,
    IReadOnlyList<LoadedMacroListItem> SequenceSnapshot,
    string? ValidationError)
{
    public bool UsesSequence => SequenceSnapshot.Count > 0;
}
