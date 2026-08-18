
namespace CrossMacro.Application.Automation;

public sealed class ManageTextExpansion(
    ITextExpansionStore store,
    IProfileManager profileManager,
    IProfileTextExpansionStore? profileStore = null) : IManageTextExpansion, IDisposable
{
    private readonly ITextExpansionStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IProfileManager _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
    private readonly IProfileTextExpansionStore? _profileStore = profileStore;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public async Task<IReadOnlyList<TextExpansionEntry>> ListAsync(string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        var expansions = await WithProfileAsync(
            profileIdentifier,
            (load, _) => load(),
            cancellationToken).ConfigureAwait(false);
        return expansions.AsReadOnly();
    }

    public async Task<TextExpansionEntry> AddAsync(TextExpansionEntry expansion, string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        return await WithProfileAsync(profileIdentifier, async (load, save) =>
        {
            var expansions = await load().ConfigureAwait(false);
            if (expansions.Any(item => string.Equals(item.Trigger, expansion.Trigger, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Text expansion trigger already exists: {expansion.Trigger}");
            }

            expansions.Add(expansion);
            await save(expansions).ConfigureAwait(false);
            return expansion;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TextExpansionEntry> RemoveAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        return await WithProfileAsync(profileIdentifier, async (load, save) =>
        {
            var expansions = await load().ConfigureAwait(false);
            var expansion = FindEntry(expansions, trigger) ?? throw new KeyNotFoundException($"No text expansion found with trigger: {trigger}");
            _ = expansions.Remove(expansion);
            await save(expansions).ConfigureAwait(false);
            return expansion;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TextExpansionEntry> SetEnabledAsync(string trigger, bool enabled, string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        return await WithProfileAsync(profileIdentifier, async (load, save) =>
        {
            var expansions = await load().ConfigureAwait(false);
            var expansion = FindEntry(expansions, trigger) ?? throw new KeyNotFoundException($"No text expansion found with trigger: {trigger}");
            expansion.IsEnabled = enabled;
            await save(expansions).ConfigureAwait(false);
            return expansion;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TextExpansionEntry?> FindAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        return await WithProfileAsync(
            profileIdentifier,
            async (load, _) => FindEntry(await load().ConfigureAwait(false), trigger),
            cancellationToken).ConfigureAwait(false);
    }

    private Task<IList<TextExpansionEntry>> LoadCurrentAsync()
    {
        if (_store is ICachedTextExpansionStore cachedStore && cachedStore.IsLoaded)
        {
            return Task.FromResult(cachedStore.GetCurrent());
        }

        return _store.LoadAsync();
    }

    private Task SaveCurrentAsync(IEnumerable<TextExpansionEntry> expansions) => _store.SaveAsync(expansions);

    private async Task<T> WithProfileAsync<T>(
        string? profileIdentifier,
        Func<Func<Task<IList<TextExpansionEntry>>>, Func<IEnumerable<TextExpansionEntry>, Task>, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var activeProfileId = _profileManager.ActiveProfile.Id;
            if (string.IsNullOrWhiteSpace(profileIdentifier))
            {
                return await operation(LoadCurrentAsync, SaveCurrentAsync).ConfigureAwait(false);
            }

            var profile = _profileManager.Profiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, profileIdentifier, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.Name, profileIdentifier, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException($"Unknown profile: {profileIdentifier}");

            var profileDirectory = _profileManager.GetProfileDirectory(profile.Id);
            if (_profileStore is not null && !string.Equals(profile.Id, activeProfileId, StringComparison.OrdinalIgnoreCase))
            {
                return await operation(
                    () => _profileStore.LoadAsync(profileDirectory, cancellationToken),
                    expansions => _profileStore.SaveAsync(profileDirectory, expansions, cancellationToken)).ConfigureAwait(false);
            }

            try
            {
                await _store.ReloadAsync(profileDirectory).ConfigureAwait(false);
                return await operation(LoadCurrentAsync, SaveCurrentAsync).ConfigureAwait(false);
            }
            finally
            {
                await _store.ReloadAsync(_profileManager.GetProfileDirectory(activeProfileId)).ConfigureAwait(false);
            }
        }
        finally
        {
            _ = _operationGate.Release();
        }
    }

    private static TextExpansionEntry? FindEntry(IEnumerable<TextExpansionEntry> expansions, string trigger) =>
        expansions.FirstOrDefault(item => string.Equals(item.Trigger, trigger, StringComparison.OrdinalIgnoreCase));

    public void Dispose() => _operationGate.Dispose();
}
