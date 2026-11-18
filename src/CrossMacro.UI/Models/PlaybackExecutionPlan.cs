
namespace CrossMacro.UI.Models;

internal sealed record PlaybackExecutionPlan(
    LoadedMacroPlaybackMode Mode,
    MacroSequence? ActiveMacro,
    IReadOnlyList<LoadedMacroListItem> SequenceSnapshot,
    string? MacroWithoutEventsName)
{
    public bool UsesSequence => SequenceSnapshot.Count > 0;
}
