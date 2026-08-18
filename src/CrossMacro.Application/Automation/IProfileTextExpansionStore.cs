namespace CrossMacro.Application.Automation;

/// <summary>
/// Reads and writes text expansions for one profile without changing the
/// process-wide active storage context.
/// </summary>
/// <remarks>
/// This port exists for profile-targeted use cases. The existing
/// <see cref="ITextExpansionStore"/> remains the active-profile compatibility
/// surface used by the runtime text expansion worker.
/// </remarks>
public interface IProfileTextExpansionStore
{
    /// <summary>Loads a profile-scoped expansion snapshot.</summary>
    public Task<IList<TextExpansionEntry>> LoadAsync(
        string profileConfigDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>Saves a profile-scoped expansion snapshot atomically.</summary>
    public Task SaveAsync(
        string profileConfigDirectory,
        IEnumerable<TextExpansionEntry> expansions,
        CancellationToken cancellationToken = default);
}
