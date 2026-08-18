
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignTextExpansionStore : ITextExpansionStore
{
    private readonly Lock _sync = new();
    private List<TextExpansionEntry> _expansions = new();

    public Task<IList<TextExpansionEntry>> LoadAsync() => Task.FromResult<IList<TextExpansionEntry>>(GetCurrent());

    public Task SaveAsync(IEnumerable<TextExpansionEntry> expansions)
    {
        ArgumentNullException.ThrowIfNull(expansions);

        lock (_sync)
        {
            _expansions = expansions.Select(CloneExpansion).ToList();
        }

        return Task.CompletedTask;
    }

    public IList<TextExpansionEntry> GetCurrent()
    {
        lock (_sync)
        {
            return _expansions.Select(CloneExpansion).ToList();
        }
    }

    private static TextExpansionEntry CloneExpansion(TextExpansionEntry expansion)
    {
        return new TextExpansionEntry(expansion.Trigger, expansion.Replacement, expansion.IsEnabled, expansion.Method, expansion.InsertionMode);
    }
}
