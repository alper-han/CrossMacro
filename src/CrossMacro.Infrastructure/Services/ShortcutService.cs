
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for managing and executing shortcut-triggered macros
/// </summary>
public sealed class ShortcutService : IShortcutService, IShortcutTaskOperations, IShortcutTaskStore
{
    private readonly IMacroFileManager _fileManager;
    private readonly Func<IMacroPlayer> _playerFactory;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IWindowManager? _windowManager;
    private SynchronizationContext? _syncContext;
    private readonly Lock _lock = new();
    private bool _disposed;

    private string _shortcutsFilePath;

    // Debounce tracking
    private readonly Dictionary<Guid, DateTime> _lastTriggerTimes = new();
    private const int DebounceIntervalMs = 300;

    // Track currently executing tasks and their players for toggle behavior
    private readonly Dictionary<Guid, IMacroPlayer> _activePlayers = new();

    // Track which key codes are part of active RunWhileHeld hotkeys (main key + modifiers)
    private readonly Dictionary<Guid, HashSet<int>> _activeHotkeyKeys = new();

    public ObservableCollection<ShortcutTask> Tasks { get; } = new();

    IReadOnlyList<ShortcutTask> IShortcutTaskStore.Tasks => SnapshotTasks();

    public bool IsListening { get; private set; }

    public event EventHandler<ShortcutExecutedEventArgs>? ShortcutExecuted;
    public event EventHandler<ShortcutStartingEventArgs>? ShortcutStarting;

    private IReadOnlyList<ShortcutTask> SnapshotTasks()
    {
        lock (_lock)
        {
            return Array.AsReadOnly(Tasks.ToArray());
        }
    }

    public ShortcutService(
        IMacroFileManager fileManager,
        Func<IMacroPlayer> playerFactory,
        IGlobalHotkeyService hotkeyService,
        string? shortcutsFilePath = null,
        IWindowManager? windowManager = null)
    {
        _fileManager = fileManager;
        _playerFactory = playerFactory;
        _hotkeyService = hotkeyService;
        _windowManager = windowManager;
        _syncContext = SynchronizationContext.Current;

        _shortcutsFilePath = string.IsNullOrWhiteSpace(shortcutsFilePath)
            ? PathHelper.GetConfigFilePath(ConfigFileNames.Shortcuts)
            : shortcutsFilePath;
        EnsureSyncContext();
    }

    private void EnsureSyncContext()
    {
        if (_syncContext is null && SynchronizationContext.Current is not null)
        {
            _syncContext = SynchronizationContext.Current;
        }
    }

    public void AddTask(ShortcutTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        EnsureSyncContext();
        task.Normalize();
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
                _ = Tasks.Remove(task);
            }
        }
    }

    public void UpdateTask(ShortcutTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        EnsureSyncContext();
        lock (_lock)
        {
            var existing = Tasks.FirstOrDefault(t => t.Id == task.Id);
            if (existing is not null)
            {
                existing.Name = task.Name;
                existing.MacroFilePath = task.MacroFilePath;
                existing.HotkeyString = task.HotkeyString;
                existing.PlaybackSpeed = task.PlaybackSpeed;
                existing.IsEnabled = false;
                existing.LoopEnabled = task.LoopEnabled;
                existing.RepeatCount = task.RepeatCount;
                existing.RepeatDelayMs = task.RepeatDelayMs;
                existing.UseRandomRepeatDelay = task.UseRandomRepeatDelay;
                existing.RepeatDelayMinMs = task.RepeatDelayMinMs;
                existing.RepeatDelayMaxMs = task.RepeatDelayMaxMs;
                existing.RunWhileHeld = task.RunWhileHeld;
                existing.WindowRules.Clear();
                foreach (var rule in task.WindowRules.Where(rule => rule is not null))
                {
                    existing.WindowRules.Add(new ShortcutWindowRule
                    {
                        Field = rule.Field,
                        MatchMode = rule.MatchMode,
                        Value = rule.Value,
                    });
                }
                existing.LastStatus = task.LastStatus;
                existing.LastTriggeredTime = task.LastTriggeredTime;
                existing.Normalize();
                _ = existing.TrySetEnabled(task.IsEnabled && task.CanBeEnabled);
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
                _ = task.TrySetEnabled(enabled);
            }
        }
    }

    public void Start()
    {
        EnsureSyncContext();
        if (IsListening)
        {
            return;
        }

        _hotkeyService.RawInputReceived += OnRawInputReceived;
        _hotkeyService.RawKeyReleased += OnRawKeyReleased;
        IsListening = true;

        Log.Information("[ShortcutService] Started listening for shortcuts");
    }

    public void StopShortcuts()
    {
        EnsureSyncContext();
        List<IMacroPlayer> playersToStop;
        lock (_lock)
        {
            playersToStop = _activePlayers.Values.ToList();
            _activePlayers.Clear();
            _activeHotkeyKeys.Clear();
        }

        foreach (var player in playersToStop)
        {
            try
            {
                player.StopPlayback();
                player.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Warning(ex, "Failed to stop active shortcut playback");
            }
        }

        if (!IsListening)
        {
            return;
        }

        _hotkeyService.RawInputReceived -= OnRawInputReceived;
        _hotkeyService.RawKeyReleased -= OnRawKeyReleased;
        IsListening = false;

        Log.Information("[ShortcutService] Stopped listening for shortcuts");
    }

    private void OnRawInputReceived(object? sender, RawHotkeyInputEventArgs e)
    {
        _ = HandleRawInputAsync(e);
    }

    internal async Task HandleRawInputAsync(RawHotkeyInputEventArgs e)
    {
        List<ShortcutCandidate> candidates;
        lock (_lock)
        {
            candidates = Tasks
                .Where(task => task.IsEnabled
                    && task.CanBeEnabled
                    && string.Equals(task.HotkeyString, e.HotkeyString, StringComparison.OrdinalIgnoreCase))
                .Select(task => new ShortcutCandidate(task, task.WindowRules
                    .Where(rule => rule is not null)
                    .Select(rule => new ShortcutWindowRule
                    {
                        Field = rule.Field,
                        MatchMode = rule.MatchMode,
                        Value = rule.Value,
                    })
                    .ToArray()))
                .ToList();
        }

        if (candidates.Count is 0)
        {
            return;
        }

        var activeTask = FindActiveToggleTask(candidates.Select(candidate => candidate.Task).ToArray());
        if (activeTask is not null)
        {
            StopActiveTask(activeTask);
            return;
        }

        var pendingHeldTaskIds = RegisterPendingHeldHotkeys(candidates, e);
        var matchingTask = await ResolveMatchingTaskAsync(candidates).ConfigureAwait(false);
        if (matchingTask is null)
        {
            RemovePendingHeldHotkeys(pendingHeldTaskIds);
            return;
        }

        RemovePendingHeldHotkeys(pendingHeldTaskIds, matchingTask.RunWhileHeld ? matchingTask.Id : null);

        IMacroPlayer? playerToStop = null;
        bool shouldStart = false;

        lock (_lock)
        {
            if (!Tasks.Contains(matchingTask) || !matchingTask.IsEnabled)
            {
                return;
            }

            if (matchingTask.RunWhileHeld && _activePlayers.ContainsKey(matchingTask.Id))
            {
                return;
            }

            if (matchingTask.RunWhileHeld
                && (!pendingHeldTaskIds.Contains(matchingTask.Id)
                    || !_activeHotkeyKeys.ContainsKey(matchingTask.Id)))
            {
                return;
            }

            if (_activePlayers.TryGetValue(matchingTask.Id, out playerToStop))
            {
                _ = _activePlayers.Remove(matchingTask.Id);
            }
            else
            {
                // Debounce check for starting
                var now = DateTime.UtcNow;
                if (_lastTriggerTimes.TryGetValue(matchingTask.Id, out var lastTime) && (now - lastTime).TotalMilliseconds < DebounceIntervalMs)
                {
                    return;
                }
                _lastTriggerTimes[matchingTask.Id] = now;
                shouldStart = true;
            }
        }

        if (playerToStop is not null)
        {
            playerToStop.StopPlayback();
            SafeUpdate(() => matchingTask.LastStatus = "Stopped");
            return;
        }

        if (shouldStart)
        {
            _ = ExecuteTaskAsync(
                matchingTask,
                requiresHeldHotkey: matchingTask.RunWhileHeld,
                cancellationToken: CancellationToken.None);
        }
    }

    private HashSet<Guid> RegisterPendingHeldHotkeys(
        IEnumerable<ShortcutCandidate> candidates,
        RawHotkeyInputEventArgs input)
    {
        HashSet<Guid> registeredTaskIds = [];
        lock (_lock)
        {
            foreach (var taskId in candidates
                         .Select(candidate => candidate.Task)
                         .Where(task => task.RunWhileHeld)
                         .Select(task => task.Id))
            {
                if (_activePlayers.ContainsKey(taskId) || _activeHotkeyKeys.ContainsKey(taskId))
                {
                    continue;
                }

                _activeHotkeyKeys[taskId] = new HashSet<int>(input.PressedModifiers) { input.KeyCode };
                _ = registeredTaskIds.Add(taskId);
            }
        }

        return registeredTaskIds;
    }

    private void RemovePendingHeldHotkeys(IReadOnlySet<Guid> taskIds, Guid? taskIdToKeep = null)
    {
        lock (_lock)
        {
            foreach (var taskId in taskIds.Where(taskId => taskId != taskIdToKeep))
            {
                _ = _activeHotkeyKeys.Remove(taskId);
            }
        }
    }

    private ShortcutTask? FindActiveToggleTask(IReadOnlyList<ShortcutTask> candidates)
    {
        lock (_lock)
        {
            return candidates.FirstOrDefault(task => !task.RunWhileHeld && _activePlayers.ContainsKey(task.Id));
        }
    }

    private void StopActiveTask(ShortcutTask task)
    {
        IMacroPlayer? player;
        lock (_lock)
        {
            if (!_activePlayers.TryGetValue(task.Id, out player))
            {
                return;
            }

            _ = _activePlayers.Remove(task.Id);
        }

        Log.Information("[ShortcutService] Stopping {TaskName} - toggle triggered", task.Name);
        player.StopPlayback();
        SafeUpdate(() => task.LastStatus = "Stopped");
    }

    private async Task<ShortcutTask?> ResolveMatchingTaskAsync(IReadOnlyList<ShortcutCandidate> candidates)
    {
        var scopedCandidates = candidates.Where(candidate => candidate.Rules.Length is > 0).ToList();
        if (scopedCandidates.Count is 0)
        {
            return candidates[0].Task;
        }

        if (_windowManager?.IsSupported is not true)
        {
            return candidates.FirstOrDefault(candidate => candidate.Rules.Length is 0)?.Task;
        }

        WindowInfo? window;
        try
        {
            window = await _windowManager.GetActiveWindowAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "Failed to query the active window for scoped shortcut matching");
            return candidates.FirstOrDefault(candidate => candidate.Rules.Length is 0)?.Task;
        }

        var scopedMatch = scopedCandidates.FirstOrDefault(candidate => candidate.Rules.Any(rule =>
            WindowRuleMatcher.IsMatch(
                rule.Field,
                rule.MatchMode,
                rule.Value,
                window?.Title,
                window?.Class,
                window?.ProcessName)));
        return scopedMatch?.Task ?? candidates.FirstOrDefault(candidate => candidate.Rules.Length is 0)?.Task;
    }

    private sealed record ShortcutCandidate(ShortcutTask Task, ShortcutWindowRule[] Rules);

    private void OnRawKeyReleased(object? sender, RawHotkeyInputEventArgs e)
    {
        List<(Guid taskId, IMacroPlayer player)> playersToStop = new();
        List<Guid> hotkeysToRemove = new();

        lock (_lock)
        {
            // Find all RunWhileHeld tasks where the released key is part of the hotkey
            foreach (var (taskId, hotkeyKeys) in _activeHotkeyKeys)
            {
                if (hotkeyKeys.Contains(e.KeyCode))
                {
                    if (_activePlayers.TryGetValue(taskId, out var player))
                    {
                        playersToStop.Add((taskId, player));
                        _ = _activePlayers.Remove(taskId);
                    }
                    hotkeysToRemove.Add(taskId);
                }
            }

            foreach (var taskId in hotkeysToRemove)
            {
                _ = _activeHotkeyKeys.Remove(taskId);
            }
        }

        foreach (var (_, player) in playersToStop)
        {
            player.StopPlayback();
        }
    }

    private bool IsHeldHotkeyActive(Guid taskId)
    {
        lock (_lock)
        {
            return _activeHotkeyKeys.ContainsKey(taskId);
        }
    }

    private async Task ExecuteTaskAsync(
        ShortcutTask task,
        bool requiresHeldHotkey = false,
        CancellationToken cancellationToken = default)
    {
        IMacroPlayer? player = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (requiresHeldHotkey && !IsHeldHotkeyActive(task.Id))
            {
                return;
            }

            if (string.IsNullOrEmpty(task.MacroFilePath) || !File.Exists(task.MacroFilePath))
            {
                SafeUpdate(() =>
                {
                    task.LastStatus = "Macro file not found";
                    task.LastTriggeredTime = DateTime.UtcNow;
                });

                ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, success: false, "Macro file not found"));
                return;
            }

            var macro = await _fileManager.LoadAsync(task.MacroFilePath).ConfigureAwait(false);
            if (requiresHeldHotkey && !IsHeldHotkeyActive(task.Id))
            {
                return;
            }
            if (macro is null)
            {
                SafeUpdate(() =>
                {
                    task.LastStatus = "Failed to load macro";
                    task.LastTriggeredTime = DateTime.UtcNow;
                });
                ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, success: false, "Failed to load macro"));
                return;
            }

            SafeUpdate(() =>
            {
                task.LastStatus = "Running...";
                task.LastTriggeredTime = DateTime.UtcNow;
            });
            ShortcutStarting?.Invoke(this, new ShortcutStartingEventArgs(task));

            player = _playerFactory();

            // Register the player so it can be stopped via toggle
            lock (_lock)
            {
                if (requiresHeldHotkey && !_activeHotkeyKeys.ContainsKey(task.Id))
                {
                    return;
                }

                _activePlayers[task.Id] = player;
            }

            var loop = task.RunWhileHeld || task.LoopEnabled;
            int repeatCount;
            if (task.RunWhileHeld)
            {
                repeatCount = 0;
            }
            else if (task.LoopEnabled)
            {
                repeatCount = task.RepeatCount;
            }
            else
            {
                repeatCount = 1;
            }
            var options = new PlaybackOptions
            {
                SpeedMultiplier = PlaybackOptions.NormalizeSpeedMultiplier(task.PlaybackSpeed),
                Loop = loop,
                RepeatCount = repeatCount,
                RepeatDelayMs = task.RepeatDelayMs,
                UseRandomRepeatDelay = task.UseRandomRepeatDelay,
                RepeatDelayMinMs = task.RepeatDelayMinMs,
                RepeatDelayMaxMs = task.RepeatDelayMaxMs,
            };

            await player.PlayAsync(macro, options, cancellationToken).ConfigureAwait(false);

            SafeUpdate(() =>
            {
                task.LastTriggeredTime = DateTime.UtcNow;
                task.LastStatus = "Success";
            });

            ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, success: true, "Executed successfully"));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("progress", StringComparison.Ordinal))
        {
            SafeUpdate(() =>
            {
                task.LastStatus = "Skipped (playback busy)";
            });
            ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, success: false, "Playback was busy"));
        }
        catch (OperationCanceledException)
        {
            // Playback was stopped/cancelled - this is expected for toggle stop
            SafeUpdate(() =>
            {
                task.LastStatus = "Stopped";
            });
            ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, success: true, "Stopped by user"));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SafeUpdate(() =>
            {
                task.LastStatus = $"Error: {ex.Message}";
                task.LastTriggeredTime = DateTime.UtcNow;
            });
            ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, success: false, ex.Message));
            Log.LogError(ex, "[ShortcutService] Error executing shortcut task {TaskName}", task.Name);
        }
        finally
        {
            // Always cleanup
            lock (_lock)
            {
                _ = _activePlayers.Remove(task.Id);
            }
            player?.Dispose();
        }
    }

    private void SafeUpdate(Action action)
    {
        if (_syncContext is not null)
        {
            _syncContext.Post(_ => action(), state: null);
        }
        else
        {
            action();
        }
    }

    public async Task SaveAsync()
    {
        EnsureSyncContext();
        try
        {
            List<ShortcutTask> taskSnapshot;
            lock (_lock)
            {
                taskSnapshot = Tasks.ToList();
            }

            await FileBackedJsonStorage.WriteAsync(
                    _shortcutsFilePath,
                    taskSnapshot,
                    CrossMacroJsonContext.Default.ListShortcutTask)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "Failed to save shortcut tasks to {Path}", _shortcutsFilePath);
            throw;
        }
    }

    public async Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ShortcutTask? task;
        lock (_lock)
        {
            task = Tasks.FirstOrDefault(x => x.Id == taskId);
        }

        if (task is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await ExecuteTaskAsync(task, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadAsync()
    {
        EnsureSyncContext();
        try
        {
            if (!File.Exists(_shortcutsFilePath))
            {
                return;
            }

            var tasks = await FileBackedJsonStorage.ReadAsync(
                    _shortcutsFilePath,
                    CrossMacroJsonContext.Default.ListShortcutTask)
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
                            task.Normalize();
                            Tasks.Add(task);
                        }
                    }
                }

                await ExecuteOnCapturedContextAsync(UpdateCollection).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "Failed to load shortcut tasks from {Path}", _shortcutsFilePath);
        }
    }

    public async Task ReloadAsync(string profileConfigDirectory)
    {
        EnsureSyncContext();
        var shortcutsFilePath = Path.Combine(profileConfigDirectory, ConfigFileNames.Shortcuts);

        lock (_lock)
        {
            _shortcutsFilePath = shortcutsFilePath;
        }

        void ClearCollection(object? state)
        {
            lock (_lock)
            {
                Tasks.Clear();
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
                if (!completion.TrySetResult())
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (!completion.TrySetException(ex))
                {
                    return;
                }
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

        StopShortcuts();
    }
}
