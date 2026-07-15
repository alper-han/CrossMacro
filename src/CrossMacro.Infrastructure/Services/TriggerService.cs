
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Polls the active window and runs configured <see cref="TriggerTask"/> actions on match.
/// </summary>
public class TriggerService : ITriggerService
{
    private readonly IWindowManager? _windowManager;
    // Factory delegate to break the circular dependency between IProfileManager and ITriggerService.
    private readonly Func<IProfileManager> _profileManagerAccessor;
    private readonly IMacroFileManager _macroFileManager;
    private readonly Func<IMacroPlayer> _macroPlayerFactory;
    private SynchronizationContext? _syncContext;
    private readonly Lock _lock = new();
    private bool _isMonitoring;
    private bool _disposed;
    private CancellationTokenSource? _cts;
    private Task _monitorTask = Task.CompletedTask;

    private string _triggersFilePath;

    /// <summary>
    /// Tracks the matching state of tasks from the previous poll for change detection.
    /// </summary>
    private readonly Dictionary<Guid, bool> _wasMatching = new();

    /// <summary>
    /// Tracks when each task first matched to determine if the debounce window has elapsed.
    /// </summary>
    private readonly Dictionary<Guid, DateTime> _firstMatchedAt = new();

    private const int PollIntervalMs = 1000;

    public ObservableCollection<TriggerTask> Tasks { get; } = new();
    public bool IsMonitoring => _isMonitoring;

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

    public event EventHandler<TriggerFiredEventArgs>? TriggerFired;

    public TriggerService(
        IWindowManager? windowManager,
        Func<IProfileManager> profileManagerAccessor,
        IMacroFileManager macroFileManager,
        Func<IMacroPlayer> macroPlayerFactory,
        string? triggersFilePath = null)
    {
        _windowManager = windowManager;
        _profileManagerAccessor = profileManagerAccessor;
        _macroFileManager = macroFileManager;
        _macroPlayerFactory = macroPlayerFactory;
        _syncContext = SynchronizationContext.Current;

        _triggersFilePath = string.IsNullOrWhiteSpace(triggersFilePath)
            ? PathHelper.GetConfigFilePath(ConfigFileNames.Triggers)
            : triggersFilePath;
        EnsureSyncContext();
    }

    private void EnsureSyncContext()
    {
        if (_syncContext is null && SynchronizationContext.Current is not null)
        {
            _syncContext = SynchronizationContext.Current;
        }
    }

    public void AddTask(TriggerTask task)
    {
        EnsureSyncContext();
        lock (_lock)
        {
            Tasks.Add(task);
        }
    }

    public void RemoveTask(Guid id)
    {
        EnsureSyncContext();
        lock (_lock)
        {
            var task = Tasks.FirstOrDefault(t => t.Id == id);
            if (task is not null)
            {
                Tasks.Remove(task);
                _wasMatching.Remove(id);
            }
        }
    }

    public void UpdateTask(TriggerTask task)
    {
        EnsureSyncContext();
        lock (_lock)
        {
            var existing = Tasks.FirstOrDefault(t => t.Id == task.Id);
            if (existing is not null)
            {
                existing.Name = task.Name;
                existing.Field = task.Field;
                existing.MatchMode = task.MatchMode;
                existing.Value = task.Value;
                existing.Action = task.Action;
                existing.TargetProfileId = task.TargetProfileId;
                existing.FireMode = task.FireMode;
                existing.MacroFilePath = task.MacroFilePath;
                existing.CooldownMs = task.CooldownMs;
                existing.DebounceMs = task.DebounceMs;
                existing.IsEnabled = task.IsEnabled;
            }
        }
    }

    public void SetTaskEnabled(Guid id, bool enabled)
    {
        EnsureSyncContext();
        lock (_lock)
        {
            var task = Tasks.FirstOrDefault(t => t.Id == id);
            if (task is not null)
            {
                task.TrySetEnabled(enabled);
                if (!enabled)
                {
                    _wasMatching.Remove(id);
                }
            }
        }
    }

    public void Start()
    {
        EnsureSyncContext();
        Task monitorTask;
        CancellationTokenSource monitorCts;

        lock (_lock)
        {
            if (_isMonitoring)
            {
                return;
            }

            _isMonitoring = true;
            _cts = new CancellationTokenSource();
            monitorCts = _cts;
            _monitorTask = Task.Run(() => MonitorLoopAsync(monitorCts.Token));
            monitorTask = _monitorTask;
        }
        _ = ObserveMonitorTaskAsync(monitorTask, monitorCts);
    }

    public void StopMonitoring()
    {
        _ = StopAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        EnsureSyncContext();
        CancellationTokenSource? cts;
        Task monitorTask;

        lock (_lock)
        {
            if (!_isMonitoring)
            {
                return;
            }
            _isMonitoring = false;
            cts = _cts;
            _cts = null;
            monitorTask = _monitorTask;
        }

        if (cts is null)
        {
            return;
        }

        try { cts.Cancel(); }
        catch (ObjectDisposedException) { }

        await CompleteStopAsync(monitorTask, cts, cancellationToken).ConfigureAwait(false);

        lock (_lock)
        {
            _wasMatching.Clear();
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
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Trigger monitoring loop faulted");
        }
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(PollIntervalMs));
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await PollOnceAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Trigger poll iteration failed");
                }

                try
                {
                    await timer.WaitForNextTickAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Single poll tick — internal for deterministic unit tests.
    /// Tests call this directly instead of waiting on the real-time timer.
    /// </summary>
    internal async Task PollOnceAsync(CancellationToken ct)
    {
        if (_windowManager is null)
        {
            return;
        }

        WindowInfo? window;
        try
        {
            window = await _windowManager.GetActiveWindowAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
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
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to query active workspace for trigger poll");
            }
        }

        // Track the first match timestamp per task for debounce verification.
        foreach (var task in snapshot)
        {
            ct.ThrowIfCancellationRequested();

            var matched = Matches(task, window, workspace, StringComparison.Ordinal);

            bool shouldFire;
            lock (_lock)
            {
                _wasMatching.TryGetValue(task.Id, out var was);

                shouldFire = task.FireMode switch
                {
                    TriggerFireMode.EveryMatch => matched,
                    TriggerFireMode.OnceOnChange => matched && !was,
                    TriggerFireMode.OnEnter => matched && !was,
                    TriggerFireMode.OnExit => !matched && was,
                    _ => matched && !was,
                };

                // Debounce: if transitioning into match, record first-matched-at and
                // suppress firing until DebounceMs elapses. On a clean signal we'd fire
                // now, but for jittery sources this prevents spurious triggers.
                if (task.DebounceMs is { } debounceMs && debounceMs > 0)
                {
                    if (matched && !was)
                    {
                        _firstMatchedAt[task.Id] = DateTime.UtcNow;
                        shouldFire = false;
                    }
                    else if (matched && was
                             && _firstMatchedAt.TryGetValue(task.Id, out var firstSeen)
                             && DateTime.UtcNow - firstSeen < TimeSpan.FromMilliseconds(debounceMs))
                    {
                        shouldFire = false;
                    }
                    else if (matched && was
                             && _firstMatchedAt.TryGetValue(task.Id, out var firstSeen2)
                             && DateTime.UtcNow - firstSeen2 >= TimeSpan.FromMilliseconds(debounceMs))
                    {
                        // Stable match survived the debounce window — allow fire this once.
                        // Reset the debounce tracking timestamp now that the trigger has fired.
                        _firstMatchedAt.Remove(task.Id);
                        if (task.FireMode is TriggerFireMode.OnceOnChange
or TriggerFireMode.OnEnter)
                        {
                            shouldFire = true;
                        }
                    }
                }

                // Cooldown: suppress fire if last triggered within CooldownMs.
                if (shouldFire
                    && task.CooldownMs is { } cdMs && cdMs > 0
                    && task.LastTriggeredTime is { } last
                    && DateTime.UtcNow - last < TimeSpan.FromMilliseconds(cdMs))
                {
                    shouldFire = false;
                }

                if (matched)
                {
                    _wasMatching[task.Id] = true;
                }
                else
                {
                    _wasMatching.Remove(task.Id);
                    _firstMatchedAt.Remove(task.Id);
                }
            }

            if (!shouldFire)
            {
                continue;
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
        string? workspace,
        StringComparison comparison)
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

        // Prevent ReDoS on user-defined pattern.
        if (task.MatchMode is TriggerMatchMode.Regex)
        {
            try
            {
                return Regex.IsMatch(actual, task.Value, RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(200));
            }
            catch (ArgumentException)
            {
                // Invalid pattern — treat as non-match rather than crashing the poll loop.
                return false;
            }
        }

        return task.MatchMode is TriggerMatchMode.Equals
            ? string.Equals(actual, task.Value, comparison)
            : actual.Contains(task.Value, comparison);
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
                    await _profileManagerAccessor().SwitchProfileAsync(task.TargetProfileId).ConfigureAwait(false);
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
        catch (Exception ex)
        {
            message = ex.Message;
            Log.Warning(ex, "Trigger action failed for task {TaskId}", task.Id);
        }

        var timestamp = DateTime.UtcNow;
        var finalMessage = message;
        void UpdateTaskState(object? _)
        {
            task.LastTriggeredTime = timestamp;
            task.LastStatus = finalMessage;
        }

        await ExecuteOnCapturedContextAsync(UpdateTaskState).ConfigureAwait(false);

        RaiseTriggerFired(new TriggerFiredEventArgs(task, success, finalMessage));
    }

    private void RaiseTriggerFired(TriggerFiredEventArgs args)
    {
        void Raise(object? _)
        {
            try { TriggerFired?.Invoke(this, args); }
            catch (Exception ex) { Log.Warning(ex, "TriggerFired subscriber threw"); }
        }

        if (_syncContext is not null)
        {
            _syncContext.Post(Raise, state: null);
        }
        else
        {
            Raise(_: null);
        }
    }

    public async Task SaveAsync()
    {
        EnsureSyncContext();
        try
        {
            List<TriggerTask> snapshot;
            lock (_lock)
            {
                snapshot = Tasks.ToList();
            }

            await FileBackedJsonStorage.WriteAsync(
                    _triggersFilePath,
                    snapshot,
                    CrossMacroJsonContext.Default.ListTriggerTask)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save trigger tasks to {Path}", _triggersFilePath);
            throw;
        }
    }

    public async Task LoadAsync()
    {
        EnsureSyncContext();
        try
        {
            if (!File.Exists(_triggersFilePath))
            {
                return;
            }

            var tasks = await FileBackedJsonStorage.ReadAsync(
                    _triggersFilePath,
                    CrossMacroJsonContext.Default.ListTriggerTask)
                .ConfigureAwait(false);

            if (tasks is not null)
            {
                void UpdateCollection(object? state)
                {
                    lock (_lock)
                    {
                        Tasks.Clear();
                        foreach (var task in tasks)
                        {
                            Tasks.Add(task);
                        }
                    }
                }

                await ExecuteOnCapturedContextAsync(UpdateCollection).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load trigger tasks from {Path}", _triggersFilePath);
        }
    }

    public async Task ReloadAsync(string profileConfigDirectory)
    {
        EnsureSyncContext();
        var triggersFilePath = Path.Combine(profileConfigDirectory, ConfigFileNames.Triggers);

        lock (_lock)
        {
            _triggersFilePath = triggersFilePath;
        }

        void ClearCollection(object? state)
        {
            lock (_lock)
            {
                Tasks.Clear();
                _wasMatching.Clear();
            }
        }

        await ExecuteOnCapturedContextAsync(ClearCollection).ConfigureAwait(false);

        await LoadAsync().ConfigureAwait(false);
    }

    private async Task ExecuteOnCapturedContextAsync(SendOrPostCallback callback)
    {
        if (_syncContext is null || SynchronizationContext.Current == _syncContext)
        {
            callback(state: null);
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _syncContext.Post(_ =>
        {
            try
            {
                callback(state: null);
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }, state: null);
        await completion.Task.ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        StopMonitoring();
    }
}
