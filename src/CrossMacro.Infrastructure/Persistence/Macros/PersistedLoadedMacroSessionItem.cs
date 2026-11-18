namespace CrossMacro.Infrastructure.Persistence.Macros;

/// <summary>
/// Infrastructure-owned representation of a loaded-macro session item.
/// </summary>
internal sealed class PersistedLoadedMacroSessionItem
{
    public Guid SessionId { get; init; }

    public string? SourcePath { get; init; }

    public int SequenceRepeatCount { get; init; } = 1;
}
