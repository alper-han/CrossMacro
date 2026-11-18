
namespace CrossMacro.Infrastructure.Services.Automation.Shortcuts;

/// <summary>
/// Service for managing and executing shortcut-triggered macros
/// </summary>
public sealed partial class ShortcutService : IShortcutService, IShortcutTaskOperations, IShortcutTaskStore
{
    private readonly IMacroFileManager _fileManager;
    private readonly Func<IMacroPlayer> _playerFactory;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IWindowManager? _windowManager;
    private readonly TimeProvider _timeProvider;
    private readonly AutomationTaskStore<ShortcutTask> _store;
    private readonly Lock _lock;
    private bool _disposed;

    private readonly IShortcutTaskRepository _taskRepository;

    // Debounce tracking
    private readonly Dictionary<Guid, long> _lastTriggerTimes = new();
    private const int DebounceIntervalMs = 300;

    // Track currently executing tasks and their players for toggle behavior
    private readonly Dictionary<Guid, IMacroPlayer> _activePlayers = new();
    private readonly HashSet<Guid> _startingTasks = [];

    // Track which key codes are part of active RunWhileHeld hotkeys (main key + modifiers)
    private readonly Dictionary<Guid, HashSet<int>> _activeHotkeyKeys = new();

    public ObservableCollection<ShortcutTask> Tasks => _store.Tasks;

    public bool IsCurrentTask(ShortcutTask task) => _store.IsCurrent(task);

    IReadOnlyList<ShortcutTask> IShortcutTaskStore.Tasks => _store.Snapshot();

    public bool IsListening { get; private set; }

    public event EventHandler<ShortcutExecutedEventArgs>? ShortcutExecuted;
    public event EventHandler<ShortcutStartingEventArgs>? ShortcutStarting;

    public ShortcutService(
        IMacroFileManager fileManager,
        Func<IMacroPlayer> playerFactory,
        IGlobalHotkeyService hotkeyService,
        IShortcutTaskRepository taskRepository,
        IWindowManager? windowManager = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(taskRepository);
        _fileManager = fileManager;
        _playerFactory = playerFactory;
        _hotkeyService = hotkeyService;
        _windowManager = windowManager;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _taskRepository = taskRepository;
        _store = new AutomationTaskStore<ShortcutTask>(new AutomationTaskStoreOptions<ShortcutTask>
        {
            Name = "shortcut",
            GetId = task => task.Id,
            Copy = AutomationTaskSnapshots.Copy,
            HasSameConfiguration = AutomationTaskSnapshots.HasSameConfiguration,
            ApplyConfiguration = AutomationTaskSnapshots.ApplyConfiguration,
            Load = taskRepository.LoadAsync,
            Save = taskRepository.SaveAsync,
            PrepareCommit = (task, live) => task.Normalize(),
            PrepareLoaded = task => task.Normalize(),
            Replacing = () =>
            {
                CancelPressReleaseSessions();
                _lastTriggerTimes.Clear();
            },
            Removed = task =>
            {
                CancelPressReleaseSessions(task.Id);
                _ = _lastTriggerTimes.Remove(task.Id);
            },
            Reconfiguring = task => CancelPressReleaseSessions(task.Id),
        });
        _lock = _store.Gate;
        _store.EnsureSyncContext();
    }

    public void Start()
    {
        _store.EnsureSyncContext();
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
        _store.EnsureSyncContext();
        List<IMacroPlayer> playersToStop;
        bool wasListening;
        lock (_lock)
        {
            wasListening = IsListening;
            IsListening = false;
            if (wasListening)
            {
                _hotkeyService.RawInputReceived -= OnRawInputReceived;
                _hotkeyService.RawKeyReleased -= OnRawKeyReleased;
            }
            CancelPressReleaseSessions();
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

        if (wasListening)
        {
            Log.Information("[ShortcutService] Stopped listening for shortcuts");
        }
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
                    && task.Hotkeys.Any(hotkey => string.Equals(hotkey, e.HotkeyString, StringComparison.OrdinalIgnoreCase)))
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
        var pendingPressRelease = RegisterPressReleaseSessions(candidates, e);
        var matchingTask = await ResolveMatchingTaskAsync(candidates).ConfigureAwait(false);
        var pressReleaseExecution = ResolvePressReleaseSessionsAsync(pendingPressRelease, matchingTask);
        if (matchingTask is null)
        {
            RemovePendingHeldHotkeys(pendingHeldTaskIds);
            return;
        }

        RemovePendingHeldHotkeys(pendingHeldTaskIds, matchingTask.RunWhileHeld ? matchingTask.Id : null);
        if (matchingTask.RunOnPressAndRelease || pressReleaseExecution is not null)
        {
            if (pressReleaseExecution is not null)
            {
                await pressReleaseExecution.ConfigureAwait(false);
            }
            return;
        }

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
                if (_startingTasks.Contains(matchingTask.Id))
                {
                    return;
                }

                // Debounce check for starting
                var now = _timeProvider.GetTimestamp();
                if (_lastTriggerTimes.TryGetValue(matchingTask.Id, out var lastTime) && _timeProvider.GetElapsedTime(lastTime, now).TotalMilliseconds < DebounceIntervalMs)
                {
                    return;
                }
                _lastTriggerTimes[matchingTask.Id] = now;
                _ = _startingTasks.Add(matchingTask.Id);
                shouldStart = true;
            }
        }

        if (playerToStop is not null)
        {
            playerToStop.StopPlayback();
            _store.Post(() => matchingTask.LastStatus = "Stopped");
            return;
        }

        if (shouldStart)
        {
            _ = await ExecuteTaskAsync(
                matchingTask,
                requiresHeldHotkey: matchingTask.RunWhileHeld,
                startReservation: true,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
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
            return candidates.FirstOrDefault(task => !task.RunWhileHeld && !task.RunOnPressAndRelease && _activePlayers.ContainsKey(task.Id));
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
        _store.Post(() => task.LastStatus = "Stopped");
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
        ReleasePressReleaseSessions(e.KeyCode);
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

    private async Task<bool> ExecuteTaskAsync(
        ShortcutTask task,
        bool requiresHeldHotkey = false,
        bool startReservation = false,
        string? macroFilePath = null,
        bool forceSingleShot = false,
        CancellationToken cancellationToken = default)
    {
        IMacroPlayer? player = null;
        var path = macroFilePath ?? task.MacroFilePath;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (requiresHeldHotkey && !IsHeldHotkeyActive(task.Id))
            {
                return false;
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _store.Post(() =>
                {
                    task.LastStatus = "Macro file not found";
                    task.LastTriggeredTime = _timeProvider.GetUtcNow().UtcDateTime;
                });

                ShortcutExecuted.RaiseSafely(this, new ShortcutExecutedEventArgs(task, success: false, "Macro file not found"), nameof(ShortcutService));
                return false;
            }

            var macro = await _fileManager.LoadAsync(path).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (requiresHeldHotkey && !IsHeldHotkeyActive(task.Id))
            {
                return false;
            }
            if (macro is null)
            {
                _store.Post(() =>
                {
                    task.LastStatus = "Failed to load macro";
                    task.LastTriggeredTime = _timeProvider.GetUtcNow().UtcDateTime;
                });
                ShortcutExecuted.RaiseSafely(this, new ShortcutExecutedEventArgs(task, success: false, "Failed to load macro"), nameof(ShortcutService));
                return false;
            }

            _store.Post(() =>
            {
                task.LastStatus = "Running...";
                task.LastTriggeredTime = _timeProvider.GetUtcNow().UtcDateTime;
            });
            ShortcutStarting.RaiseSafely(this, new ShortcutStartingEventArgs(task), nameof(ShortcutService));

            player = _playerFactory();

            // Register the player so it can be stopped via toggle
            lock (_lock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (requiresHeldHotkey && !_activeHotkeyKeys.ContainsKey(task.Id))
                {
                    return false;
                }

                _activePlayers[task.Id] = player;
            }

            var loop = !forceSingleShot && (task.RunWhileHeld || task.LoopEnabled);
            int repeatCount;
            if (forceSingleShot)
            {
                repeatCount = 1;
            }
            else if (task.RunWhileHeld)
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

            var playbackTask = player.PlayAsync(macro, options, cancellationToken);
            if (startReservation)
            {
                lock (_lock)
                {
                    _ = _startingTasks.Remove(task.Id);
                }
            }

            await playbackTask.ConfigureAwait(false);

            _store.Post(() =>
            {
                task.LastTriggeredTime = _timeProvider.GetUtcNow().UtcDateTime;
                task.LastStatus = "Success";
            });

            cancellationToken.ThrowIfCancellationRequested();
            ShortcutExecuted.RaiseSafely(this, new ShortcutExecutedEventArgs(task, success: true, "Executed successfully"), nameof(ShortcutService));
            return true;
        }
        catch (PlaybackAlreadyInProgressException)
        {
            _store.Post(() =>
            {
                task.LastStatus = "Skipped (playback busy)";
            });
            ShortcutExecuted.RaiseSafely(this, new ShortcutExecutedEventArgs(task, success: false, "Playback was busy"), nameof(ShortcutService));
            return false;
        }
        catch (OperationCanceledException)
        {
            // Playback was stopped/cancelled - this is expected for toggle stop
            _store.Post(() =>
            {
                task.LastStatus = "Stopped";
            });
            ShortcutExecuted.RaiseSafely(this, new ShortcutExecutedEventArgs(task, success: true, "Stopped by user"), nameof(ShortcutService));
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _store.Post(() =>
            {
                task.LastStatus = $"Error: {ex.Message}";
                task.LastTriggeredTime = _timeProvider.GetUtcNow().UtcDateTime;
            });
            ShortcutExecuted.RaiseSafely(this, new ShortcutExecutedEventArgs(task, success: false, ex.Message), nameof(ShortcutService));
            Log.LogError(ex, "[ShortcutService] Error executing shortcut task {TaskName}", task.Name);
            return false;
        }
        finally
        {
            // Always cleanup
            lock (_lock)
            {
                if (player is not null && _activePlayers.TryGetValue(task.Id, out var active) && ReferenceEquals(active, player))
                {
                    _ = _activePlayers.Remove(task.Id);
                }
                if (startReservation)
                {
                    _ = _startingTasks.Remove(task.Id);
                }
            }
            player?.Dispose();
        }
    }

    public Task CommitAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken = default) =>
        _store.CommitAsync(tasks, cancellationToken);

    public Task SaveAsync() => _store.SaveAsync();

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
        if (task.RunOnPressAndRelease)
        {
            PressReleaseSession session;
            lock (_lock)
            {
                session = CreatePressReleaseSession(task, cancellationToken);
                _ = session.Released.TrySetResult();
            }
            await ExecutePressReleaseSessionAsync(session, accepted: true).ConfigureAwait(false);
        }
        else
        {
            _ = await ExecuteTaskAsync(task, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    Task IShortcutTaskStore.LoadAsync() => _store.EnsureLoadedAsync();

    public Task LoadAsync() => _store.LoadAsync();

    public Task ReloadAsync(string profileConfigDirectory) =>
        _store.ReloadAsync(() => _taskRepository.SetProfileDirectory(profileConfigDirectory));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        StopShortcuts();
        _store.Dispose();
    }
}
