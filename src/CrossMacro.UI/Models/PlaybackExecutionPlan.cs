
namespace CrossMacro.UI.Models;

internal sealed record class PlaybackExecutionPlan(
    LoadedMacroPlaybackMode Mode,
    MacroSequence? ActiveMacro,
    IReadOnlyList<LoadedMacroListItem> SequenceSnapshot,
    string? ValidationError)
{
    public bool UsesSequence => SequenceSnapshot.Count > 0;
}
