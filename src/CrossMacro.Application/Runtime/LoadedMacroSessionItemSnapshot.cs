namespace CrossMacro.Application.Runtime;

/// <summary>
/// Persistence-neutral state for one loaded macro list item.
/// </summary>
public sealed record LoadedMacroSessionItemSnapshot(
    Guid SessionId,
    MacroSequence Macro,
    string? SourcePath,
    int SequenceRepeatCount);
