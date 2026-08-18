using System.ComponentModel;

namespace CrossMacro.Infrastructure.Persistence.Macros;

/// <summary>
/// Infrastructure-owned representation of profiles/{id}/loaded-macros.json.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PersistedLoadedMacroSession
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public IReadOnlyList<PersistedLoadedMacroSessionItem> Items { get; init; } = [];

    public Guid? SelectedSessionId { get; init; }

    public int PlaybackMode { get; init; }
}
