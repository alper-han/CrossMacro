
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for managing and executing shortcut-triggered macros
/// </summary>
public class ShortcutService : IShortcutService
{
    private readonly IMacroFileManager _fileManager;
    private readonly Func<IMacroPlayer> _playerFactory;
    private readonly IGlobalHotkeyService _hotkeyService;
    private SynchronizationContext? _syncContext;
    private readonly Lock _lock = new();
    private bool _isListening;
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
    public bool IsListening => _isListening;

    public event EventHandler<ShortcutExecutedEventArgs>? ShortcutExecuted;
    public event EventHandler<ShortcutStartingEventArgs>? ShortcutStarting;

    public ShortcutService(
        IMacroFileManager fileManager,
        Func<IMacroPlayer> playerFactory,
        IGlobalHotkeyService hotkeyService,
        string? shortcutsFilePath = null)
    {
        _fileManager = fileManager;
        _playerFactory = playerFactory;
        _hotkeyService = hotkeyService;
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
                Tasks.Remove(task);
            }
        }
    }

    public void UpdateTask(ShortcutTask task)
    {
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
                existing.LastStatus = task.LastStatus;
                existing.LastTriggeredTime = task.LastTriggeredTime;
                existing.Normalize();
                existing.TrySetEnabled(task.IsEnabled);
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
            }
        }
    }

    public void Start()
    {
        EnsureSyncContext();
        if (_isListening)
        {
            return;
        }

        _hotkeyService.RawInputReceived += OnRawInputReceived;
        _hotkeyService.RawKeyReleased += OnRawKeyReleased;
        _isListening = true;

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
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to stop active shortcut playback");
            }
        }

        if (!_isListening)
        {
            return;
        }

        _hotkeyService.RawInputReceived -= OnRawInputReceived;
        _hotkeyService.RawKeyReleased -= OnRawKeyReleased;
        _isListening = false;

        Log.Information("[ShortcutService] Stopped listening for shortcuts");
    }

    private void OnRawInputReceived(object? sender, RawHotkeyInputEventArgs e)
    {
        ShortcutTask? matchingTask = null;
        IMacroPlayer? playerToStop = null;
        bool shouldStart = false;

        lock (_lock)
        {
            // Find matching enabled task
            matchingTask = Tasks.FirstOrDefault(t =>
                t.IsEnabled &&
                string.Equals(t.HotkeyString, e.HotkeyString, StringComparison.OrdinalIgnoreCase));

            if (matchingTask is null)
            {
                return;
            }

            if (matchingTask.RunWhileHeld && _activePlayers.ContainsKey(matchingTask.Id))
            {
                return;
            }

            if (!matchingTask.RunWhileHeld && _activePlayers.TryGetValue(matchingTask.Id, out playerToStop))
            {
                Log.Information("[ShortcutService] Stopping {TaskName} - toggle triggered", matchingTask.Name);
                _activePlayers.Remove(matchingTask.Id);
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

        // Stop player outside lock
        if (playerToStop is not null)
        {
            playerToStop.StopPlayback();
            SafeUpdate(() => matchingTask.LastStatus = "Stopped");
            return;
        }

        if (shouldStart)
        {
            // For RunWhileHeld, track all keys that make up this hotkey
            if (matchingTask.RunWhileHeld)
            {
                lock (_lock)
                {
                    _activeHotkeyKeys[matchingTask.Id] = new HashSet<int>(e.PressedModifiers) { e.KeyCode };
                }
            }
            _ = ExecuteTaskAsync(matchingTask);
        }
    }

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
                        _activePlayers.Remove(taskId);
                    }
                    hotkeysToRemove.Add(taskId);
                }
            }

            foreach (var taskId in hotkeysToRemove)
            {
                _activeHotkeyKeys.Remove(taskId);
            }
        }

        foreach (var (_, player) in playersToStop)
        {
            player.StopPlayback();
        }
    }

    private async Task ExecuteTaskAsync(ShortcutTask task, CancellationToken cancellationToken = default)
    {
        IMacroPlayer? player = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                _activePlayers[task.Id] = player;
            }

            var loop = task.RunWhileHeld || task.LoopEnabled;
            var repeatCount = task.RunWhileHeld ? 0 : (task.LoopEnabled ? task.RepeatCount : 1);
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
        catch (Exception ex)
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
                _activePlayers.Remove(task.Id);
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
        catch (Exception ex)
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
        await ExecuteTaskAsync(task, cancellationToken).ConfigureAwait(false);
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
        catch (Exception ex)
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

        StopShortcuts();
    }
}
