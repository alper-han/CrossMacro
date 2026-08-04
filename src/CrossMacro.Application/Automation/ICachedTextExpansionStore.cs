namespace CrossMacro.Application.Automation;

/// <summary>
/// Exposes an already-loaded text-expansion snapshot to consumers that need to
/// avoid re-reading the same profile file during startup.
/// </summary>
public interface ICachedTextExpansionStore
{
    public bool IsLoaded { get; }

    public IList<TextExpansionEntry> GetCurrent();
}
