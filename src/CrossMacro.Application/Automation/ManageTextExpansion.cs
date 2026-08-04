
namespace CrossMacro.Application.Automation;

public sealed class ManageTextExpansion(ITextExpansionStore store, IProfileManager profileManager) : IManageTextExpansion
{
    private readonly ITextExpansionStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IProfileManager _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));

    public async Task<IReadOnlyList<TextExpansionEntry>> ListAsync(string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        var expansions = await WithProfileAsync(profileIdentifier, LoadCurrentAsync, cancellationToken).ConfigureAwait(false);
        return expansions.AsReadOnly();
    }

    public async Task<TextExpansionEntry> AddAsync(TextExpansionEntry expansion, string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        return await WithProfileAsync(profileIdentifier, async () =>
        {
            var expansions = await LoadCurrentAsync().ConfigureAwait(false);
            if (expansions.Any(item => string.Equals(item.Trigger, expansion.Trigger, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Text expansion trigger already exists: {expansion.Trigger}");
            }

            expansions.Add(expansion);
            await _store.SaveAsync(expansions).ConfigureAwait(false);
            return expansion;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TextExpansionEntry> RemoveAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        return await WithProfileAsync(profileIdentifier, async () =>
        {
            var expansions = await LoadCurrentAsync().ConfigureAwait(false);
            var expansion = FindEntry(expansions, trigger) ?? throw new KeyNotFoundException($"No text expansion found with trigger: {trigger}");
            _ = expansions.Remove(expansion);
            await _store.SaveAsync(expansions).ConfigureAwait(false);
            return expansion;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TextExpansionEntry> SetEnabledAsync(string trigger, bool enabled, string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        return await WithProfileAsync(profileIdentifier, async () =>
        {
            var expansions = await LoadCurrentAsync().ConfigureAwait(false);
            var expansion = FindEntry(expansions, trigger) ?? throw new KeyNotFoundException($"No text expansion found with trigger: {trigger}");
            expansion.IsEnabled = enabled;
            await _store.SaveAsync(expansions).ConfigureAwait(false);
            return expansion;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TextExpansionEntry?> FindAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        return await WithProfileAsync(profileIdentifier, async () =>
            FindEntry(await LoadCurrentAsync().ConfigureAwait(false), trigger), cancellationToken).ConfigureAwait(false);
    }

    private Task<IList<TextExpansionEntry>> LoadCurrentAsync()
    {
        if (_store is ICachedTextExpansionStore cachedStore && cachedStore.IsLoaded)
        {
            return Task.FromResult(cachedStore.GetCurrent());
        }

        return _store.LoadAsync();
    }

    private async Task<T> WithProfileAsync<T>(string? profileIdentifier, Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var activeProfileId = _profileManager.ActiveProfile.Id;
        if (string.IsNullOrWhiteSpace(profileIdentifier))
        {
            return await operation().ConfigureAwait(false);
        }

        var profile = _profileManager.Profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, profileIdentifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Name, profileIdentifier, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown profile: {profileIdentifier}");

        await _store.ReloadAsync(_profileManager.GetProfileDirectory(profile.Id)).ConfigureAwait(false);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            await _store.ReloadAsync(_profileManager.GetProfileDirectory(activeProfileId)).ConfigureAwait(false);
        }
    }

    private static TextExpansionEntry? FindEntry(IEnumerable<TextExpansionEntry> expansions, string trigger) =>
        expansions.FirstOrDefault(item => string.Equals(item.Trigger, trigger, StringComparison.OrdinalIgnoreCase));
}
