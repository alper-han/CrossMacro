namespace CrossMacro.Application.Runtime;

/// <summary>
/// Reads and writes the complete loaded-macro session for one profile.
/// </summary>
public interface IProfileLoadedMacroSessionStore
{
    /// <summary>Loads a profile-scoped session snapshot, or an empty snapshot when none exists.</summary>
    public Task<LoadedMacroSessionSnapshot> LoadAsync(
        string profileConfigDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>Saves a profile-scoped session snapshot atomically.</summary>
    public Task SaveAsync(
        string profileConfigDirectory,
        LoadedMacroSessionSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
