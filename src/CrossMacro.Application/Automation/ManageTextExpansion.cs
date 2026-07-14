using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Application.Automation;

public interface ITextExpansionStore
{
    Task<List<TextExpansion>> LoadAsync();
    Task ReloadAsync(string profileConfigDirectory) => LoadAsync();
    Task SaveAsync(IEnumerable<TextExpansion> expansions);
}

public interface IManageTextExpansion
{
    Task<IReadOnlyList<TextExpansion>> ListAsync(string? profileIdentifier = null, CancellationToken cancellationToken = default);
    Task<TextExpansion> AddAsync(TextExpansion expansion, string? profileIdentifier = null, CancellationToken cancellationToken = default);
    Task<TextExpansion> RemoveAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default);
    Task<TextExpansion> SetEnabledAsync(string trigger, bool enabled, string? profileIdentifier = null, CancellationToken cancellationToken = default);
    Task<TextExpansion?> FindAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default);
}

public sealed class ManageTextExpansion : IManageTextExpansion
{
    private readonly ITextExpansionStore _store;
    private readonly IProfileManager _profileManager;

    public ManageTextExpansion(ITextExpansionStore store, IProfileManager profileManager)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
    }

    public async Task<IReadOnlyList<TextExpansion>> ListAsync(string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        return await WithProfileAsync(profileIdentifier, cancellationToken, () => _store.LoadAsync()).ConfigureAwait(false);
    }

    public async Task<TextExpansion> AddAsync(TextExpansion expansion, string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        return await WithProfileAsync(profileIdentifier, cancellationToken, async () =>
        {
            var expansions = await _store.LoadAsync().ConfigureAwait(false);
            if (expansions.Any(item => string.Equals(item.Trigger, expansion.Trigger, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Text expansion trigger already exists: {expansion.Trigger}");
            }

            expansions.Add(expansion);
            await _store.SaveAsync(expansions).ConfigureAwait(false);
            return expansion;
        }).ConfigureAwait(false);
    }

    public async Task<TextExpansion> RemoveAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        return await WithProfileAsync(profileIdentifier, cancellationToken, async () =>
        {
            var expansions = await _store.LoadAsync().ConfigureAwait(false);
            var expansion = Find(expansions, trigger) ?? throw new KeyNotFoundException($"No text expansion found with trigger: {trigger}");
            expansions.Remove(expansion);
            await _store.SaveAsync(expansions).ConfigureAwait(false);
            return expansion;
        }).ConfigureAwait(false);
    }

    public async Task<TextExpansion> SetEnabledAsync(string trigger, bool enabled, string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        return await WithProfileAsync(profileIdentifier, cancellationToken, async () =>
        {
            var expansions = await _store.LoadAsync().ConfigureAwait(false);
            var expansion = Find(expansions, trigger) ?? throw new KeyNotFoundException($"No text expansion found with trigger: {trigger}");
            expansion.IsEnabled = enabled;
            await _store.SaveAsync(expansions).ConfigureAwait(false);
            return expansion;
        }).ConfigureAwait(false);
    }

    public async Task<TextExpansion?> FindAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default)
    {
        return await WithProfileAsync(profileIdentifier, cancellationToken, async () =>
            Find(await _store.LoadAsync().ConfigureAwait(false), trigger)).ConfigureAwait(false);
    }

    private async Task<T> WithProfileAsync<T>(string? profileIdentifier, CancellationToken cancellationToken, Func<Task<T>> operation)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var activeProfileId = _profileManager.ActiveProfile.Id;
        if (string.IsNullOrWhiteSpace(profileIdentifier))
        {
            return await operation().ConfigureAwait(false);
        }

        var profile = _profileManager.Profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, profileIdentifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Name, profileIdentifier, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            throw new KeyNotFoundException($"Unknown profile: {profileIdentifier}");
        }

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

    private static TextExpansion? Find(IEnumerable<TextExpansion> expansions, string trigger) =>
        expansions.FirstOrDefault(item => string.Equals(item.Trigger, trigger, StringComparison.OrdinalIgnoreCase));
}
