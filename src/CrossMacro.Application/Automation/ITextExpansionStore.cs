namespace CrossMacro.Application.Automation;

/// <summary>Active-profile text expansion storage.</summary>
public interface ITextExpansionStore
{
    /// <summary>Whether <see cref="GetCurrent"/> reflects a completed load of the active profile.</summary>
    public bool IsLoaded { get; }

    public Task<IList<TextExpansionEntry>> LoadAsync();

    /// <summary>Returns the already-loaded expansions without touching the file.</summary>
    public IList<TextExpansionEntry> GetCurrent();

    public Task SaveAsync(IEnumerable<TextExpansionEntry> expansions);
}
