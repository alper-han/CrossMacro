
namespace CrossMacro.Infrastructure.Services.Automation.Shortcuts;

/// <summary>
/// Service for managing and executing shortcut-triggered macros
/// </summary>
public sealed partial class ShortcutService : IShortcutService, IShortcutTaskOperations, IAutomationTaskStore<ShortcutTask>
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

    IReadOnlyList<ShortcutTask> IAutomationTaskStore<ShortcutTask>.Tasks => _store.Snapshot();

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
        var candidates = CollectCandidates(e.HotkeyString);

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

        var (playerToStop, shouldStart) = ReserveHotkeyAction(matchingTask, pendingHeldTaskIds);
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

    private List<ShortcutCandidate> CollectCandidates(string hotkey)
    {
        lock (_lock)
        {
            return Tasks
                .Where(task => task.IsEnabled
                    && task.CanBeEnabled
                    && task.Hotkeys.Any(configured => string.Equals(configured, hotkey, StringComparison.OrdinalIgnoreCase)))
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
    }

    /// <summary>
    /// Under the lock, decides whether a matched hotkey stops a running player or reserves a start slot.
    /// Neither is set when the press must be ignored.
    /// </summary>
    private (IMacroPlayer? PlayerToStop, bool ShouldStart) ReserveHotkeyAction(ShortcutTask matchingTask, HashSet<Guid> pendingHeldTaskIds)
    {
        lock (_lock)
        {
            if (!Tasks.Contains(matchingTask) || !matchingTask.IsEnabled)
            {
                return default;
            }

            if (matchingTask.RunWhileHeld && _activePlayers.ContainsKey(matchingTask.Id))
            {
                return default;
            }

            if (matchingTask.RunWhileHeld
                && (!pendingHeldTaskIds.Contains(matchingTask.Id)
                    || !_activeHotkeyKeys.ContainsKey(matchingTask.Id)))
            {
                return default;
            }

            if (_activePlayers.Remove(matchingTask.Id, out var playerToStop))
            {
                return (playerToStop, ShouldStart: false);
            }

            if (_startingTasks.Contains(matchingTask.Id))
            {
                return default;
            }

            // Debounce check for starting
            var now = _timeProvider.GetTimestamp();
            if (_lastTriggerTimes.TryGetValue(matchingTask.Id, out var lastTime) && _timeProvider.GetElapsedTime(lastTime, now).TotalMilliseconds < DebounceIntervalMs)
            {
                return default;
            }

            _lastTriggerTimes[matchingTask.Id] = now;
            _ = _startingTasks.Add(matchingTask.Id);
            return (null, ShouldStart: true);
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

    Task IAutomationTaskStore<ShortcutTask>.LoadAsync() => _store.EnsureLoadedAsync();

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
