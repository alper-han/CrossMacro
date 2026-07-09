using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Helpers;
using CrossMacro.Infrastructure.Serialization;
using CrossMacro.Platform.Abstractions;

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
        if (_syncContext == null && SynchronizationContext.Current != null)
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
            if (task != null)
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
            if (existing != null)
            {
                existing.Name = task.Name;
                existing.Field = task.Field;
                existing.MatchMode = task.MatchMode;
                existing.Value = task.Value;
                existing.Action = task.Action;
                existing.TargetProfileId = task.TargetProfileId;
                existing.FireMode = task.FireMode;
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
            if (task != null)
            {
                task.IsEnabled = enabled;
                if (!enabled) _wasMatching.Remove(id);
            }
        }
    }

    public void Start()
    {
        EnsureSyncContext();
        CancellationToken ct;

        lock (_lock)
        {
            if (_isMonitoring) return;
            _isMonitoring = true;
            _cts = new CancellationTokenSource();
            ct = _cts.Token;
        }

        _ = Task.Run(() => MonitorLoopAsync(ct));
    }

    public void Stop()
    {
        EnsureSyncContext();
        CancellationTokenSource? cts;

        lock (_lock)
        {
            if (!_isMonitoring) return;
            _isMonitoring = false;
            cts = _cts;
            _cts = null;
        }

        if (cts == null) return;

        try { cts.Cancel(); }
        catch (ObjectDisposedException) { }

        // Dispose token source after confirming the monitoring loop has exited.
        _ = CompleteStopAsync(cts);

        lock (_lock)
        {
            _wasMatching.Clear();
        }
    }

    private static async Task CompleteStopAsync(CancellationTokenSource cts)
    {
        try
        {
            // Brief yield so any in-flight MonitorLoopAsync await sees cancellation
            // then re-enters its loop and observes IsCancellationRequested.
            await Task.Yield();
        }
        catch { }

        cts.Dispose();
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
        if (_windowManager is null) return;
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
        var anyWorkspaceTask = snapshot.Exists(t => t.IsEnabled && t.Field == TriggerField.Workspace);
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
                        if (task.FireMode == TriggerFireMode.OnceOnChange
                            || task.FireMode == TriggerFireMode.OnEnter)
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

            if (!shouldFire) continue;

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
        if (task.Field == TriggerField.None)
        {
            return true;
        }
        else if (task.Field == TriggerField.Workspace)
        {
            actual = workspace;
        }
        else if (task.Field == TriggerField.ProcessName)
        {
            actual = window?.ProcessName;
        }
        else if (task.Field == TriggerField.WindowClass)
        {
            actual = window?.Class;
        }
        else // WindowTitle
        {
            actual = window?.Title;
        }

        if (string.IsNullOrEmpty(actual)) return false;

        // Prevent ReDoS on user-defined pattern.
        if (task.MatchMode == TriggerMatchMode.Regex)
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

        return task.MatchMode == TriggerMatchMode.Equals
            ? string.Equals(actual, task.Value, comparison)
            : actual.Contains(task.Value, comparison);
    }

    private async Task ExecuteActionAsync(TriggerTask task, CancellationToken ct)
    {
        var success = false;
        string? message = null;

        try
        {
            if (task.Action == TriggerAction.SwitchProfile)
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
                    if (macro == null)
                    {
                        message = "Failed to load macro";
                    }
                    else
                    {
                        using var player = _macroPlayerFactory();
                        await player.PlayAsync(macro, null, ct).ConfigureAwait(false);
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

        if (_syncContext != null) _syncContext.Post(UpdateTaskState, null);
        else UpdateTaskState(null);

        RaiseTriggerFired(new TriggerFiredEventArgs(task, success, finalMessage));
    }

    private void RaiseTriggerFired(TriggerFiredEventArgs args)
    {
        void Raise(object? _)
        {
            try { TriggerFired?.Invoke(this, args); }
            catch (Exception ex) { Log.Warning(ex, "TriggerFired subscriber threw"); }
        }

        if (_syncContext != null) _syncContext.Post(Raise, null);
        else Raise(null);
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
            if (!File.Exists(_triggersFilePath)) return;

            var tasks = await FileBackedJsonStorage.ReadAsync(
                    _triggersFilePath,
                    CrossMacroJsonContext.Default.ListTriggerTask)
                .ConfigureAwait(false);

            if (tasks != null)
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

                if (_syncContext != null) _syncContext.Post(UpdateCollection, null);
                else UpdateCollection(null);
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

        if (_syncContext != null) _syncContext.Post(ClearCollection, null);
        else ClearCollection(null);

        await LoadAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
    }
}
