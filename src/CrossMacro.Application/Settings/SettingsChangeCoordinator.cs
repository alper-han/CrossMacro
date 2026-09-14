namespace CrossMacro.Application.Settings;

/// <summary>Owns changed-field application and rollback across coalesced persistence operations.</summary>
public sealed class SettingsChangeCoordinator(ISettingsService settingsService)
{
    private readonly ISettingsService _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    private readonly Lock _gate = new();
    private readonly Dictionary<string, long> _effectVersions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _fieldVersions = new(StringComparer.Ordinal);
    private readonly Dictionary<Task, SaveBatch> _batches = new();
    private long _nextVersion;
    private int _generation;

    public void InvalidatePendingChanges()
    {
        lock (_gate) { _generation++; _fieldVersions.Clear(); _effectVersions.Clear(); }
    }

    public Task CommitAsync(SettingsChangeRequest request, CancellationToken cancellationToken = default) =>
        CommitAsync(request, SettingsSaveMode.Immediate, cancellationToken);

    public Task CommitAsync(SettingsChangeRequest request, SettingsSaveMode saveMode, CancellationToken cancellationToken = default)
        => CommitAsync(request, saveMode, afterPersist: null, cancellationToken);

    public Task CommitAsync(SettingsChangeRequest request, SettingsSaveMode saveMode, Func<Task>? afterPersist, CancellationToken cancellationToken = default)
        => CommitWithEffectAsync(request, saveMode, afterPersist is null ? null : _ => afterPersist(), cancellationToken);

    public Task CommitWithEffectAsync(SettingsChangeRequest request, SettingsSaveMode saveMode, Func<Func<bool>, Task>? afterPersist, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Before is null || request.After is null)
        { throw new ArgumentException("Both settings snapshots are required.", nameof(request)); }
        cancellationToken.ThrowIfCancellationRequested();
        var before = AppSettingsSnapshot.Copy(request.Before);
        var after = AppSettingsSnapshot.Copy(request.After);
        after.Normalize();
        var fields = AppSettingsSnapshot.Fields.Where(field => !field.Equal(before, after)).ToArray();
        if (fields.Length is 0) { return Task.CompletedTask; }

        Task save;
        SaveBatch batch;
        EffectGuard effectGuard;
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var field in fields) { field.Copy(after, _settingsService.Current); }
            try
            {
                save = saveMode is SettingsSaveMode.AfterIdle
                    ? _settingsService.SaveAfterIdleAsync()
                    : _settingsService.SaveAsync();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                foreach (var field in fields) { field.Copy(before, _settingsService.Current); }
                return Task.FromException(error);
            }

            if (!_batches.TryGetValue(save, out batch!))
            {
                batch = new SaveBatch(_generation);
                // A newer persistence snapshot also contains all still-pending field changes.
                foreach (var pending in _batches.Values.Where(pending => pending.Generation == _generation))
                {
                    foreach (var change in pending.Changes.Values)
                    {
                        if (_fieldVersions.GetValueOrDefault(change.Field.Key) == change.Version)
                        {
                            var version = ++_nextVersion;
                            _fieldVersions[change.Field.Key] = version;
                            batch.Changes[change.Field.Key] = change with { After = AppSettingsSnapshot.Copy(_settingsService.Current), Version = version };
                        }
                    }
                }
                _batches.Add(save, batch);
            }
            foreach (var field in fields)
            {
                var version = ++_nextVersion;
                _fieldVersions[field.Key] = version;
                _effectVersions[field.Key] = version;
                if (batch.Changes.TryGetValue(field.Key, out var previous))
                {
                    batch.Changes[field.Key] = previous with { After = after, Version = version };
                }
                else
                {
                    batch.Changes.Add(field.Key, new FieldChange(field, before, after, version));
                }
            }
            effectGuard = new EffectGuard(_generation, after, fields.ToDictionary(field => field.Key, field => _effectVersions[field.Key], StringComparer.Ordinal));
        }
        return ObserveSaveAsync(save, batch, afterPersist, effectGuard);
    }

    private async Task ObserveSaveAsync(Task save, SaveBatch batch, Func<Func<bool>, Task>? afterPersist, EffectGuard effectGuard)
    {
        try
        {
            await save.ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            lock (_gate)
            {
                if (batch.Generation == _generation)
                {
                    foreach (var change in batch.Changes.Values)
                    {
                        if (_fieldVersions.TryGetValue(change.Field.Key, out var version)
                            && version == change.Version
                            && change.Field.Equal(_settingsService.Current, change.After))
                        {
                            change.Field.Copy(change.Before, _settingsService.Current);
                        }
                    }
                }
            }
            throw;
        }
        finally
        {
            lock (_gate) { _ = _batches.Remove(save); }
        }

        // Persistence has committed. A runtime effect failure must not revert persisted settings.
        if (afterPersist is not null && IsCurrent(effectGuard))
        { await afterPersist(() => IsCurrent(effectGuard)).ConfigureAwait(false); }
    }

    private bool IsCurrent(EffectGuard effect)
    {
        lock (_gate)
        {
            return effect.Generation == _generation && effect.Versions.All(pair =>
                _effectVersions.GetValueOrDefault(pair.Key) == pair.Value
                && AppSettingsSnapshot.Fields.Single(field => string.Equals(field.Key, pair.Key, StringComparison.Ordinal)).Equal(_settingsService.Current, effect.After));
        }
    }

    private sealed record EffectGuard(int Generation, AppSettings After, IReadOnlyDictionary<string, long> Versions);

    private sealed class SaveBatch(int generation)
    {
        public int Generation { get; } = generation;
        public Dictionary<string, FieldChange> Changes { get; } = new(StringComparer.Ordinal);
    }

    private sealed record FieldChange(SettingsField Field, AppSettings Before, AppSettings After, long Version);
}
