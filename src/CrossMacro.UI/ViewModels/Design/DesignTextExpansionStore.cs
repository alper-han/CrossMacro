
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignTextExpansionStore : ITextExpansionStore, IProfileTextExpansionStore
{
    private readonly Lock _sync = new();
    private readonly Dictionary<string, List<TextExpansionEntry>> _profiles = new(StringComparer.Ordinal);
    private List<TextExpansionEntry> _expansions = new();

    public bool IsLoaded => true;

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

    public Task<IList<TextExpansionEntry>> LoadAsync(string profileConfigDirectory, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var stored = _profiles.TryGetValue(profileConfigDirectory, out var existing) ? existing : [];
            return Task.FromResult<IList<TextExpansionEntry>>(stored.Select(CloneExpansion).ToList());
        }
    }

    public Task SaveAsync(string profileConfigDirectory, IEnumerable<TextExpansionEntry> expansions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expansions);

        lock (_sync)
        {
            _profiles[profileConfigDirectory] = expansions.Select(CloneExpansion).ToList();
        }

        return Task.CompletedTask;
    }

    private static TextExpansionEntry CloneExpansion(TextExpansionEntry expansion)
    {
        return new TextExpansionEntry(expansion.Trigger, expansion.Replacement, expansion.IsEnabled, expansion.Method, expansion.InsertionMode);
    }
}
