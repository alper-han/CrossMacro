namespace CrossMacro.Application.Runtime;

/// <summary>
/// Persistence-neutral state for the loaded macro list of one profile.
/// </summary>
public sealed record LoadedMacroSessionSnapshot(
    IReadOnlyList<LoadedMacroSessionItemSnapshot> Items,
    Guid? SelectedSessionId,
    int PlaybackMode)
{
    public static LoadedMacroSessionSnapshot Empty { get; } = new(Items: [], SelectedSessionId: null, PlaybackMode: 0);
}
