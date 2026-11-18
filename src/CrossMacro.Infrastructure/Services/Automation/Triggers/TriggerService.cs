
namespace CrossMacro.Infrastructure.Services.Automation.Triggers;

/// <summary>
/// Polls the active window and runs configured <see cref="TriggerTask"/> actions on match.
/// </summary>
public sealed class TriggerService : ITriggerService, ITriggerTaskStore, IAsyncDisposable
{
    private readonly IWindowManager? _windowManager;
    private readonly IProfileSwitchRequests _profileSwitchRequests;
    private readonly IMacroFileManager _macroFileManager;
    private readonly Func<IMacroPlayer> _macroPlayerFactory;
    private readonly TimeProvider _timeProvider;
    private readonly AutomationTaskStore<TriggerTask> _store;
    private readonly Lock _lock;
    private readonly ITriggerTaskRepository _taskRepository;
    private TaskCompletionSource? _disposeCompletion;
    private CancellationTokenSource? _cts;
    private Task _monitorTask = Task.CompletedTask;
    private Task _profileSwitchTask = Task.CompletedTask;
    private long _monitorGeneration;

    /// <summary>
    /// Tracks the matching state of tasks from the previous poll for change detection.
    /// </summary>
    private readonly Dictionary<Guid, bool> _wasMatching = new();

    /// <summary>
    /// Tracks when each task first matched to determine if the debounce window has elapsed.
    /// </summary>
    private readonly Dictionary<Guid, DateTime> _firstMatchedAt = new();

    private const int PollIntervalMs = 1000;

    public ObservableCollection<TriggerTask> Tasks => _store.Tasks;

    public bool IsCurrentTask(TriggerTask task) => _store.IsCurrent(task);

    IReadOnlyList<TriggerTask> ITriggerTaskStore.Tasks => _store.Snapshot();

    public bool IsMonitoring { get; private set; }

    public Task Completion
    {
        get
        {
            lock (_lock)
            {
                return _monitorTask;
            }
        }
    }

    // A profile switch can stop this monitor; it must not be part of monitor completion.
    internal Task ProfileSwitchCompletion { get { lock (_lock) { return _profileSwitchTask; } } }

    public event EventHandler<TriggerFiredEventArgs>? TriggerFired;

    public TriggerService(
        IWindowManager? windowManager,
        IProfileSwitchRequests profileSwitchRequests,
        IMacroFileManager macroFileManager,
        Func<IMacroPlayer> macroPlayerFactory,
        TimeProvider timeProvider,
        ITriggerTaskRepository taskRepository)
    {
        ArgumentNullException.ThrowIfNull(taskRepository);
        _windowManager = windowManager;
        _profileSwitchRequests = profileSwitchRequests;
        _macroFileManager = macroFileManager;
        _macroPlayerFactory = macroPlayerFactory;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _taskRepository = taskRepository;
        _store = new AutomationTaskStore<TriggerTask>(new AutomationTaskStoreOptions<TriggerTask>
        {
            Name = "trigger",
            GetId = task => task.Id,
            Copy = AutomationTaskSnapshots.Copy,
            HasSameConfiguration = AutomationTaskSnapshots.HasSameConfiguration,
            ApplyConfiguration = AutomationTaskSnapshots.ApplyConfiguration,
            Load = taskRepository.LoadAsync,
            Save = taskRepository.SaveAsync,
            PrepareCommit = (task, live) => _ = task.TrySetEnabled(task.IsEnabled),
            Replacing = () =>
            {
                _wasMatching.Clear();
                _firstMatchedAt.Clear();
            },
            Removed = task =>
            {
                _ = _wasMatching.Remove(task.Id);
                _ = _firstMatchedAt.Remove(task.Id);
            },
        });
        _lock = _store.Gate;
        _store.EnsureSyncContext();
    }

    public void Start()
    {
        _store.EnsureSyncContext();
        Task monitorTask;
        CancellationTokenSource monitorCts;

        lock (_lock)
        {
            if (IsMonitoring)
            {
                return;
            }

            IsMonitoring = true;
            _monitorGeneration++;
            _cts = new CancellationTokenSource();
            monitorCts = _cts;
            // No token for Task.Run: cancel-before-start would make Completion transition to
            // Canceled; MonitorLoopAsync already ends gracefully on cancellation.
            _monitorTask = Task.Run(() => MonitorLoopAsync(monitorCts.Token), CancellationToken.None);
            monitorTask = _monitorTask;
        }
        _ = ObserveMonitorTaskAsync(monitorTask, monitorCts);
    }

    public void StopMonitoring()
    {
        _ = StopAsync(CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _store.EnsureSyncContext();
        CancellationTokenSource? cts;
        Task monitorTask;
        long monitorGeneration;

        lock (_lock)
        {
            if (!IsMonitoring)
            {
                return;
            }
            IsMonitoring = false;
            cts = _cts;
            _cts = null;
            monitorTask = _monitorTask;
            monitorGeneration = _monitorGeneration;
        }

        if (cts is null)
        {
            return;
        }

        try { await cts.CancelAsync().ConfigureAwait(false); }
        catch (ObjectDisposedException)
        {
            Log.Debug("[TriggerService] Cancellation source was already disposed while stopping.");
        }

        var completionCancellationToken = cancellationToken == cts.Token
            ? CancellationToken.None
            : cancellationToken;
        await CompleteStopAsync(monitorTask, cts, completionCancellationToken).ConfigureAwait(false);

        lock (_lock)
        {
            if (_monitorGeneration == monitorGeneration)
            {
                _wasMatching.Clear();
            }
        }
    }

    private static async Task CompleteStopAsync(Task monitorTask, CancellationTokenSource cts, CancellationToken cancellationToken)
    {
        try
        {
            await monitorTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Log.Debug("[TriggerService] Monitor task canceled during shutdown.");
        }
        finally
        {
            cts.Dispose();
        }
    }

    private static async Task ObserveMonitorTaskAsync(Task monitorTask, CancellationTokenSource cts)
    {
        try
        {
            await monitorTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Cancellation is expected while the monitor is shutting down.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Trigger monitoring loop faulted");
        }
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(PollIntervalMs), _timeProvider);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await PollOnceAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.Warning(ex, "Trigger poll iteration failed");
                }

                try
                {
                    _ = await timer.WaitForNextTickAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Log.Debug("[TriggerService] Poll timer canceled.");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Debug("[TriggerService] Monitor loop canceled.");
        }
    }

    /// <summary>
    /// Single poll tick — internal for deterministic unit tests.
    /// Tests call this directly instead of waiting on the real-time timer.
    /// </summary>
    internal async Task PollOnceAsync(CancellationToken ct)
    {
        lock (_lock) { if (!_profileSwitchTask.IsCompleted) { return; } }
        if (_windowManager?.IsSupported is not true)
        {
            return;
        }

        WindowInfo? window;
        try
        {
            window = await _windowManager.GetActiveWindowAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "Failed to query active window for trigger poll");
            return;
        }

        // Lazily fetch the active workspace only if an enabled task requires it.
        string? workspace = null;
        var snapshot = TasksWithSnapshot();
        var anyWorkspaceTask = snapshot.Exists(t => t.IsEnabled && t.Field is TriggerField.Workspace);
        if (anyWorkspaceTask)
        {
            try
            {
                workspace = await _windowManager.GetActiveWorkspaceAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Warning(ex, "Failed to query active workspace for trigger poll");
            }
        }

        // Track the first match timestamp per task for debounce verification.
        foreach (var task in snapshot)
        {
            ct.ThrowIfCancellationRequested();

            var matched = Matches(task, window, workspace);

            bool shouldFire;
            lock (_lock)
            {
                _ = _wasMatching.TryGetValue(task.Id, out var was);

                shouldFire = task.FireMode switch
                {
                    TriggerFireMode.EveryMatch => matched,
                    TriggerFireMode.OnceOnChange => matched && !was,
                    TriggerFireMode.OnEnter => matched && !was,
                    TriggerFireMode.OnExit => !matched && was,
                    _ => matched && !was,
                };

                // Debounce: a new match only fires once it has stayed stable for DebounceMs.
                if (task.DebounceMs is { } debounceMs && debounceMs > 0)
                {
                    var now = _timeProvider.GetUtcNow().UtcDateTime;
                    if (matched && !was)
                    {
                        _firstMatchedAt[task.Id] = now;
                        shouldFire = false;
                    }
                    else if (matched && was && _firstMatchedAt.TryGetValue(task.Id, out var firstSeen))
                    {
                        if (now - firstSeen < TimeSpan.FromMilliseconds(debounceMs))
                        {
                            shouldFire = false;
                        }
                        else
                        {
                            _ = _firstMatchedAt.Remove(task.Id);
                            shouldFire |= task.FireMode is TriggerFireMode.OnceOnChange or TriggerFireMode.OnEnter;
                        }
                    }
                }

                // Cooldown: suppress fire if last triggered within CooldownMs.
                if (shouldFire
                    && task.CooldownMs is { } cdMs && cdMs > 0
                    && task.LastTriggeredTime is { } last
                    && _timeProvider.GetUtcNow().UtcDateTime - last < TimeSpan.FromMilliseconds(cdMs))
                {
                    shouldFire = false;
                }

                if (matched)
                {
                    _wasMatching[task.Id] = true;
                }
                else
                {
                    _ = _wasMatching.Remove(task.Id);
                    _ = _firstMatchedAt.Remove(task.Id);
                }
            }

            if (!shouldFire)
            {
                continue;
            }

            if (task.Action is TriggerOperation.SwitchProfile)
            {
                // The awaited profile request may quiesce this very monitor. Own it
                // separately so the poll can return and the monitor can settle first.
                lock (_lock) { _profileSwitchTask = ExecuteActionAsync(task, CancellationToken.None); }
                return;
            }
            await ExecuteActionAsync(task, ct).ConfigureAwait(false);
        }
    }

    private List<TriggerTask> TasksWithSnapshot()
    {
        lock (_lock)
        {
            return Tasks.Where(t => t.IsEnabled).ToList();
        }
    }

    private static bool Matches(
        TriggerTask task,
        WindowInfo? window,
        string? workspace)
    {
        // None matches unconditionally. Workspace matches the active workspace.
        string? actual;
        if (task.Field is TriggerField.None)
        {
            return true;
        }

        if (task.Field is TriggerField.Workspace)
        {
            actual = workspace;
        }
        else if (task.Field is TriggerField.ProcessName)
        {
            actual = window?.ProcessName;
        }
        else if (task.Field is TriggerField.WindowClass)
        {
            actual = window?.Class;
        }
        else // WindowTitle
        {
            actual = window?.Title;
        }

        if (string.IsNullOrEmpty(actual))
        {
            return false;
        }

        return WindowRuleMatcher.IsMatch(task.MatchMode, task.Value, actual);
    }

    private async Task ExecuteActionAsync(TriggerTask task, CancellationToken ct)
    {
        var success = false;
        string? message = null;

        try
        {
            if (task.Action is TriggerOperation.SwitchProfile)
            {
                if (string.IsNullOrEmpty(task.TargetProfileId))
                {
                    message = "Target profile not set";
                }
                else
                {
                    await _profileSwitchRequests.RequestSwitchAsync(task.TargetProfileId).ConfigureAwait(false);
                    success = true;
                    message = $"Switched to profile '{task.TargetProfileId}'";
                }
            }
            else // RunMacro
            {
                if (string.IsNullOrEmpty(task.MacroFilePath) || !File.Exists(task.MacroFilePath))
                {
                    message = "Macro file not found";
                }
                else
                {
                    var macro = await _macroFileManager.LoadAsync(task.MacroFilePath).ConfigureAwait(false);
                    if (macro is null)
                    {
                        message = "Failed to load macro";
                    }
                    else
                    {
                        using var player = _macroPlayerFactory();
                        await player.PlayAsync(macro, options: null, ct).ConfigureAwait(false);
                        success = true;
                        message = $"Ran macro '{task.MacroFilePath}'";
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            message = ex.Message;
            Log.Warning(ex, "Trigger action failed for task {TaskId}", task.Id);
        }

        var timestamp = _timeProvider.GetUtcNow().UtcDateTime;
        var finalMessage = message;
        await _store.ExecuteOnContextAsync(() =>
        {
            task.LastTriggeredTime = timestamp;
            task.LastStatus = finalMessage;
        }).ConfigureAwait(false);

        RaiseTriggerFired(new TriggerFiredEventArgs(task, success, finalMessage));
    }

    private void RaiseTriggerFired(TriggerFiredEventArgs args) =>
        _store.Post(() => TriggerFired.RaiseSafely(this, args, nameof(TriggerService)));

    public Task CommitAsync(IReadOnlyList<TriggerTask> tasks, CancellationToken cancellationToken = default) =>
        _store.CommitAsync(tasks, cancellationToken);

    public Task SaveAsync() => _store.SaveAsync();

    Task ITriggerTaskStore.LoadAsync() => _store.EnsureLoadedAsync();

    public Task LoadAsync() => _store.LoadAsync();

    public Task ReloadAsync(string profileConfigDirectory) =>
        _store.ReloadAsync(() => _taskRepository.SetProfileDirectory(profileConfigDirectory));

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>Stops polling and waits for the poll loop to finish without blocking the caller.</summary>
    public async ValueTask DisposeAsync()
    {
        TaskCompletionSource disposeCompletion;
        var initiateDispose = false;

        lock (_lock)
        {
            if (_disposeCompletion is null)
            {
                _disposeCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                initiateDispose = true;
            }

            disposeCompletion = _disposeCompletion;
        }

        if (initiateDispose)
        {
            try
            {
                await StopAsync(CancellationToken.None).ConfigureAwait(false);
                await Completion.ConfigureAwait(false);
                _store.Dispose();
                _ = disposeCompletion.TrySetResult();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _ = disposeCompletion.TrySetException(ex);
                throw;
            }
        }

        await disposeCompletion.Task.ConfigureAwait(false);
    }
}
