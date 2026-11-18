namespace CrossMacro.Application.Automation;

/// <summary>
/// Reads and edits text expansions of the active or another profile. Every operation runs under the
/// automation mutation gate, so it cannot interleave with a profile switch.
/// </summary>
public sealed class ManageTextExpansion(
    ITextExpansionStore store,
    IProfileManager profileManager,
    IProfileTextExpansionStore profileStore,
    AutomationTaskMutationGate mutationGate) : IManageTextExpansion
{
    private readonly ITextExpansionStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IProfileManager _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
    private readonly IProfileTextExpansionStore _profileStore = profileStore ?? throw new ArgumentNullException(nameof(profileStore));
    private readonly AutomationTaskMutationGate _mutationGate = mutationGate ?? throw new ArgumentNullException(nameof(mutationGate));

    public async Task<IReadOnlyList<TextExpansionEntry>> ListAsync(string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        var expansions = await WithTargetAsync(profileIdentifier, target => target.Load(), cancellationToken).ConfigureAwait(false);
        return expansions.AsReadOnly();
    }

    public Task<TextExpansionEntry> AddAsync(TextExpansionEntry expansion, string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expansion);
        return WithTargetAsync(profileIdentifier, async target =>
        {
            var expansions = await target.Load().ConfigureAwait(false);
            if (FindEntry(expansions, expansion.Trigger) is not null)
            {
                throw new InvalidOperationException($"Text expansion trigger already exists: {expansion.Trigger}");
            }

            expansions.Add(expansion);
            await target.Save(expansions).ConfigureAwait(false);
            return expansion;
        }, cancellationToken);
    }

    public Task<TextExpansionEntry> RemoveAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default) =>
        WithTargetAsync(profileIdentifier, async target =>
        {
            var expansions = await target.Load().ConfigureAwait(false);
            var expansion = FindEntry(expansions, trigger) ?? throw NotFound(trigger);
            _ = expansions.Remove(expansion);
            await target.Save(expansions).ConfigureAwait(false);
            return expansion;
        }, cancellationToken);

    public Task<TextExpansionEntry> SetEnabledAsync(string trigger, bool enabled, string? profileIdentifier = null, CancellationToken cancellationToken = default) =>
        WithTargetAsync(profileIdentifier, async target =>
        {
            var expansions = await target.Load().ConfigureAwait(false);
            var expansion = FindEntry(expansions, trigger) ?? throw NotFound(trigger);
            expansion.IsEnabled = enabled;
            await target.Save(expansions).ConfigureAwait(false);
            return expansion;
        }, cancellationToken);

    public Task<TextExpansionEntry?> FindAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default) =>
        WithTargetAsync(
            profileIdentifier,
            async target => FindEntry(await target.Load().ConfigureAwait(false), trigger),
            cancellationToken);

    private Task<T> WithTargetAsync<T>(string? profileIdentifier, Func<Target, Task<T>> operation, CancellationToken cancellationToken) =>
        _mutationGate.RunAsync(() => operation(ResolveTarget(profileIdentifier, cancellationToken)), cancellationToken);

    private Target ResolveTarget(string? profileIdentifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profileIdentifier))
        {
            return ActiveTarget();
        }

        var profile = _profileManager.Profiles.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, profileIdentifier, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.Name, profileIdentifier, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown profile: {profileIdentifier}");

        if (string.Equals(profile.Id, _profileManager.ActiveProfile.Id, StringComparison.OrdinalIgnoreCase))
        {
            return ActiveTarget();
        }

        var profileDirectory = _profileManager.GetProfileDirectory(profile.Id);
        return new Target(
            () => _profileStore.LoadAsync(profileDirectory, cancellationToken),
            expansions => _profileStore.SaveAsync(profileDirectory, expansions, cancellationToken));
    }

    private Target ActiveTarget() => new(
        () => _store.IsLoaded ? Task.FromResult(_store.GetCurrent()) : _store.LoadAsync(),
        _store.SaveAsync);

    private static TextExpansionEntry? FindEntry(IEnumerable<TextExpansionEntry> expansions, string trigger) =>
        expansions.FirstOrDefault(item => string.Equals(item.Trigger, trigger, StringComparison.OrdinalIgnoreCase));

    private static KeyNotFoundException NotFound(string trigger) => new($"No text expansion found with trigger: {trigger}");

    private sealed record Target(Func<Task<IList<TextExpansionEntry>>> Load, Func<IEnumerable<TextExpansionEntry>, Task> Save);
}
