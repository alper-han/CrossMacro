using System.ComponentModel;

namespace CrossMacro.Infrastructure.Persistence.Macros;

/// <summary>
/// Infrastructure-owned representation of a loaded-macro session item.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PersistedLoadedMacroSessionItem
{
    public Guid SessionId { get; init; }

    public string? SourcePath { get; init; }

    public int SequenceRepeatCount { get; init; } = 1;
}
