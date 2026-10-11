namespace CrossMacro.Application.Settings;

/// <summary>Applies changed fields and runtime effects immediately, with rollback across coalesced persistence operations.</summary>
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
        => CommitAsync(request, saveMode, applyRuntimeEffect: null, cancellationToken);

    public Task CommitAsync(SettingsChangeRequest request, SettingsSaveMode saveMode, Func<Task>? applyRuntimeEffect, CancellationToken cancellationToken = default)
        => CommitWithEffectAsync(request, saveMode, applyRuntimeEffect is null ? null : _ => applyRuntimeEffect(), cancellationToken);

    public Task CommitWithEffectAsync(SettingsChangeRequest request, SettingsSaveMode saveMode, Func<Func<bool>, Task>? applyRuntimeEffect, CancellationToken cancellationToken = default)
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

        SaveBatch? batch = null;
        EffectGuard? effectGuard = null;
        var saveTask = _settingsService.AccessCurrent(_ =>
        {
            Task save;
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
            return save;
        });
        if (batch is null || effectGuard is null) { return saveTask; }

        // Observe persistence separately: disk latency must never defer live runtime effects.
        // Only a persistence failure rolls back fields; runtime errors still surface to the caller.
        var persistence = ObserveSaveAsync(saveTask, batch);
        return applyRuntimeEffect is null
            ? persistence
            : Task.WhenAll(persistence, ApplyRuntimeEffectAsync(applyRuntimeEffect, effectGuard));
    }

    private async Task ApplyRuntimeEffectAsync(Func<Func<bool>, Task> applyRuntimeEffect, EffectGuard effectGuard)
    {
        if (IsCurrent(effectGuard))
        { await applyRuntimeEffect(() => IsCurrent(effectGuard)).ConfigureAwait(false); }
    }

    private async Task ObserveSaveAsync(Task save, SaveBatch batch)
    {
        try
        {
            await save.ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _ = _settingsService.AccessCurrent(_ =>
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
                return true;
            });
            throw;
        }
        finally
        {
            lock (_gate) { _ = _batches.Remove(save); }
        }
    }

    private bool IsCurrent(EffectGuard effect)
    {
        return _settingsService.AccessCurrent(_ =>
        {
            lock (_gate)
            {
                return effect.Generation == _generation && effect.Versions.All(pair =>
                    _effectVersions.GetValueOrDefault(pair.Key) == pair.Value
                    && AppSettingsSnapshot.Fields.Single(field => string.Equals(field.Key, pair.Key, StringComparison.Ordinal)).Equal(_settingsService.Current, effect.After));
            }
        });
    }

    private sealed record EffectGuard(int Generation, AppSettings After, IReadOnlyDictionary<string, long> Versions);

    private sealed class SaveBatch(int generation)
    {
        public int Generation { get; } = generation;
        public Dictionary<string, FieldChange> Changes { get; } = new(StringComparer.Ordinal);
    }

    private sealed record FieldChange(SettingsField Field, AppSettings Before, AppSettings After, long Version);
}
