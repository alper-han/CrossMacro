namespace CrossMacro.Infrastructure.Services.Automation;

/// <summary>
/// The live task collection of one automation service together with its persistence rules: loads, saves and
/// commits are serialized, collection changes happen on the captured UI context, and a store whose file could not
/// be loaded refuses to write so the unreadable file is never overwritten.
/// </summary>
internal sealed class AutomationTaskStore<TTask>(AutomationTaskStoreOptions<TTask> options) : IDisposable
    where TTask : class
{
    private readonly SemaphoreSlim _persistenceGate = new(1, 1);
    private readonly AutomationUiContext _uiContext = new();
    private bool _loaded;
    private bool _loadFailed;

    public Lock Gate { get; } = new();

    public ObservableCollection<TTask> Tasks { get; } = new();

    public IReadOnlyList<TTask> Snapshot()
    {
        lock (Gate)
        {
            return Array.AsReadOnly(Tasks.ToArray());
        }
    }

    public bool IsCurrent(TTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        lock (Gate)
        {
            return Tasks.Any(active => ReferenceEquals(active, task));
        }
    }

    public void EnsureSyncContext() => _uiContext.EnsureCaptured();

    /// <summary>Runs <paramref name="action"/> on the captured context without waiting for it.</summary>
    public void Post(Action action) => _uiContext.Post(action);

    public void Dispose() => _persistenceGate.Dispose();

    public Task ExecuteOnContextAsync(Action action) => _uiContext.InvokeAsync(action);

    public async Task WithGateAsync(Func<Task> operation)
    {
        await _persistenceGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            _ = _persistenceGate.Release();
        }
    }

    public Task LoadAsync() => WithGateAsync(LoadCoreAsync);

    public Task EnsureLoadedAsync() => WithGateAsync(async () =>
    {
        if (!_loaded)
        {
            await LoadCoreAsync().ConfigureAwait(false);
        }
    });

    /// <summary>Switches the repository to another profile directory and replaces the live tasks with its content.</summary>
    public Task ReloadAsync(Action setProfileDirectory) => WithGateAsync(async () =>
    {
        EnsureSyncContext();
        lock (Gate)
        {
            setProfileDirectory();
        }

        await ExecuteOnContextAsync(() =>
        {
            lock (Gate)
            {
                options.Replacing?.Invoke();
                Tasks.Clear();
            }
        }).ConfigureAwait(false);

        await LoadCoreAsync().ConfigureAwait(false);
    });

    public Task SaveAsync() => WithGateAsync(async () =>
    {
        ThrowIfLoadFailed();
        EnsureSyncContext();
        try
        {
            IReadOnlyList<TTask> snapshot;
            lock (Gate)
            {
                snapshot = Tasks.Select(options.Copy).ToList();
            }

            await options.Save(snapshot, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[AutomationTaskStore] Failed to save {Kind} tasks", options.Name);
            throw;
        }
    });

    public async Task CommitAsync(IReadOnlyList<TTask> tasks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        await _persistenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfLoadFailed();
            var snapshot = tasks.Select(options.Copy).ToList();
            if (options.PrepareCommit is { } prepare)
            {
                lock (Gate)
                {
                    var live = MatchLiveTasks(snapshot);
                    for (var index = 0; index < snapshot.Count; index++)
                    {
                        prepare(snapshot[index], live[index]);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            await options.Save(snapshot, cancellationToken).ConfigureAwait(false);
            // Persistence is the commit point. Cancellation must not leave memory behind disk.
            await ExecuteOnContextAsync(() => Publish(snapshot)).ConfigureAwait(false);
        }
        finally
        {
            _ = _persistenceGate.Release();
        }
    }

    private void Publish(List<TTask> snapshot)
    {
        lock (Gate)
        {
            var live = MatchLiveTasks(snapshot);
            var retained = live.ToHashSet(ReferenceEqualityComparer.Instance);
            for (var index = Tasks.Count - 1; index >= 0; index--)
            {
                if (!retained.Contains(Tasks[index]))
                {
                    options.Removed?.Invoke(Tasks[index]);
                    Tasks.RemoveAt(index);
                }
            }

            for (var index = 0; index < snapshot.Count; index++)
            {
                var task = snapshot[index];
                if (live[index] is not { } existing)
                {
                    Tasks.Add(task);
                    live[index] = task;
                }
                else if (!options.HasSameConfiguration(task, existing))
                {
                    options.Reconfiguring?.Invoke(existing);
                    options.ApplyConfiguration(task, existing);
                    Tasks[Tasks.IndexOf(existing)] = existing;
                }
            }

            for (var index = 0; index < snapshot.Count; index++)
            {
                var task = live[index]!;
                if (!ReferenceEquals(Tasks[index], task))
                {
                    Tasks.Move(Tasks.IndexOf(task), index);
                }
            }
        }
    }

    /// <summary>
    /// The live task each snapshot entry stands for, or null for a new task. Tasks are matched by id. A
    /// hand-edited file can repeat an id; those tasks are matched in list order so each stays editable.
    /// </summary>
    private TTask?[] MatchLiveTasks(List<TTask> snapshot)
    {
        var unmatched = new Dictionary<Guid, Queue<TTask>>(Tasks.Count);
        foreach (var task in Tasks)
        {
            var id = options.GetId(task);
            if (!unmatched.TryGetValue(id, out var sameId))
            {
                unmatched[id] = sameId = new Queue<TTask>(1);
            }

            sameId.Enqueue(task);
        }

        var live = new TTask?[snapshot.Count];
        for (var index = 0; index < snapshot.Count; index++)
        {
            if (unmatched.TryGetValue(options.GetId(snapshot[index]), out var sameId) && sameId.TryDequeue(out var task))
            {
                live[index] = task;
            }
        }

        return live;
    }

    private async Task LoadCoreAsync()
    {
        _loaded = false;
        EnsureSyncContext();
        try
        {
            var tasks = await options.Load(CancellationToken.None).ConfigureAwait(false);
            await ExecuteOnContextAsync(() =>
            {
                lock (Gate)
                {
                    options.Replacing?.Invoke();
                    Tasks.Clear();
                    foreach (var task in tasks ?? [])
                    {
                        if (task is null)
                        {
                            Log.Warning("[AutomationTaskStore] Skipping null {Kind} task entry during load", options.Name);
                            continue;
                        }

                        options.PrepareLoaded?.Invoke(task);
                        Tasks.Add(task);
                    }
                }
            }).ConfigureAwait(false);
            _loaded = true;
            _loadFailed = false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _loadFailed = true;
            Log.Warning(ex, "[AutomationTaskStore] Failed to load {Kind} tasks; saving is disabled until a load succeeds", options.Name);
        }
    }

    private void ThrowIfLoadFailed()
    {
        if (_loadFailed)
        {
            throw new InvalidOperationException($"The {options.Name} tasks could not be loaded; saving would overwrite the unreadable file.");
        }
    }
}
