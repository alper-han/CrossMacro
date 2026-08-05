
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for scheduling and executing macro tasks
/// </summary>
public sealed class SchedulerService : ISchedulerService, IScheduledTaskOperations, IScheduledTaskStore
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly IScheduledTaskRepository _repository;
    private readonly IScheduledTaskExecutor _executor;
    private readonly TimeProvider _timeProvider;
    private readonly SynchronizationContext? _syncContext;
    private readonly Lock _lock = new();
    private readonly Lock _ctsLock = new();

    private PeriodicTimer? _periodicTimer;
    private CancellationTokenSource? _cts;
    private Task? _timerTask;
    private Task? _shutdownTask;
    private bool _disposed;

    public ObservableCollection<ScheduledTask> Tasks { get; } = new();

    IReadOnlyList<ScheduledTask> IScheduledTaskStore.Tasks => SnapshotTasks();

    public bool IsRunning { get; private set; }

    public Task Completion { get
        {
            lock (_lock)
            {
                return field;
            }
        }

        private set;
    } = Task.CompletedTask;

    public event EventHandler<TaskExecutedEventArgs>? TaskExecuted;
    public event EventHandler<ScheduledTaskStartingEventArgs>? TaskStarting;

    private IReadOnlyList<ScheduledTask> SnapshotTasks()
    {
        lock (_lock)
        {
            return Array.AsReadOnly(Tasks.ToArray());
        }
    }

    public SchedulerService(
        IScheduledTaskRepository repository,
        IScheduledTaskExecutor executor,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _executor = executor;
        _timeProvider = timeProvider;
        _syncContext = SynchronizationContext.Current;

        _executor.TaskExecuted += OnExecutorTaskExecuted;
        _executor.TaskStarting += OnExecutorTaskStarting;
    }

    private void OnExecutorTaskExecuted(object? sender, TaskExecutedEventArgs e)
    {
        try { TaskExecuted?.Invoke(this, e); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warning(ex, "[SchedulerService] TaskExecuted subscriber threw"); }
    }

    private void OnExecutorTaskStarting(object? sender, ScheduledTaskStartingEventArgs e)
    {
        try { TaskStarting?.Invoke(this, e); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Log.Warning(ex, "[SchedulerService] TaskStarting subscriber threw"); }
    }

    public void AddTask(ScheduledTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        lock (_lock)
        {
            Tasks.Add(task);
            if (task.IsEnabled)
            {
                task.CalculateNextRunTime(_timeProvider.GetUtcNow().UtcDateTime);
            }
        }
    }

    public void RemoveTask(Guid id)
    {
        lock (_lock)
        {
            var task = Tasks.FirstOrDefault(t => t.Id == id);
            if (task is not null)
            {
                _ = Tasks.Remove(task);
            }
        }
    }

    public void UpdateTask(ScheduledTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        lock (_lock)
        {
            var existing = Tasks.FirstOrDefault(t => t.Id == task.Id);
            if (existing is not null)
            {
                task.Normalize();
                // Update properties instead of replacing the object instance
                // This preserves references in the UI (e.g., SelectedTask)
                existing.Name = task.Name;
                existing.MacroFilePath = task.MacroFilePath;
                existing.Type = task.Type;
                existing.PlaybackSpeed = task.PlaybackSpeed;
                existing.IntervalValue = task.IntervalValue;
                existing.IntervalUnit = task.IntervalUnit;
                existing.UseRandomIntervalDelay = task.UseRandomIntervalDelay;
                existing.IntervalMinValue = task.IntervalMinValue;
                existing.IntervalMaxValue = task.IntervalMaxValue;
                existing.ScheduledDateTime = task.ScheduledDateTime;
                existing.WeeklyDays = task.WeeklyDays;
                existing.WeeklyTime = task.WeeklyTime;
                existing.LastRunTime = task.LastRunTime;
                existing.NextRunTime = task.NextRunTime;
                existing.LastStatus = task.LastStatus;

                // Update IsEnabled last as it might trigger recalculations
                _ = existing.TrySetEnabled(task.IsEnabled);
            }
        }
    }

    public void SetTaskEnabled(Guid id, bool enabled)
    {
        lock (_lock)
        {
            var task = Tasks.FirstOrDefault(t => t.Id == id);
            if (task is not null)
            {
                _ = task.TrySetEnabled(enabled);
                if (enabled)
                {
                    task.CalculateNextRunTime(_timeProvider.GetUtcNow().UtcDateTime);
                }
                else
                {
                    task.NextRunTime = null;
                }
            }
        }
    }

    public async Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ScheduledTask? task;
        lock (_lock)
        {
            task = Tasks.FirstOrDefault(t => t.Id == taskId);
        }

        if (task is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _executor.ExecuteAsync(task, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Start()
    {
        Task? timerTask;
        CancellationTokenSource? cts;

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (IsRunning)
            {
                return;
            }

            if (_shutdownTask is { IsCompleted: true })
            {
                _shutdownTask = null;
            }

            if (_shutdownTask is not null)
            {
                Log.Warning("[SchedulerService] Start ignored while the previous scheduler lifetime is still shutting down");
                return;
            }

            IsRunning = true;
            _cts = new CancellationTokenSource();
            _periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            _timerTask = RunTimerLoopAsync(_periodicTimer, _cts.Token);
            Completion = _timerTask;

            timerTask = _timerTask;
            cts = _cts;
        }

        _ = ObserveTimerLoopAsync(timerTask, cts);
    }

    public void StopScheduler()
    {
        _ = ObserveStopAsync();
    }

    private async Task ObserveStopAsync()
    {
        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[SchedulerService] Non-blocking stop failed");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? timerTask;
        PeriodicTimer? periodicTimer;
        CancellationTokenSource? cts;
        var shouldInitiateStop = false;

        lock (_lock)
        {
            if (_shutdownTask is not null)
            {
                timerTask = _shutdownTask;
                periodicTimer = null;
                cts = null;
            }
            else if (!IsRunning && _periodicTimer is null && _cts is null && _timerTask is null)
            {
                return;
            }
            else
            {
                IsRunning = false;
                timerTask = _timerTask;
                periodicTimer = _periodicTimer;
                cts = _cts;

                _timerTask = null;
                _periodicTimer = null;
                _cts = null;
                _shutdownTask = timerTask;
                shouldInitiateStop = true;
            }
        }

        if (shouldInitiateStop)
        {
            if (cts is not null)
            {
                CancelCts(cts, "[SchedulerService] Cancellation callbacks failed while stopping");
            }

            periodicTimer?.Dispose();

            if (timerTask is null)
            {
                cts?.Dispose();
                lock (_lock)
                {
                    _shutdownTask = null;
                }
                return;
            }
        }

        await CompleteStopAsync(timerTask!, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunTimerLoopAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await CheckTasksAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping the scheduler
        }
    }

    private async Task ObserveTimerLoopAsync(Task timerTask, CancellationTokenSource cts)
    {
        try
        {
            await timerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Log.Debug("[SchedulerService] Timer loop canceled during shutdown.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            bool shouldCleanup;
            lock (_lock)
            {
                shouldCleanup = ReferenceEquals(_timerTask, timerTask);
                if (shouldCleanup)
                {
                    IsRunning = false;
                    _timerTask = null;
                    _periodicTimer = null;
                    _cts = null;
                }
            }

            Log.LogError(ex, "[SchedulerService] Timer loop faulted and scheduler was stopped");

            if (shouldCleanup)
            {
                CancelCts(cts, "[SchedulerService] Timer cancellation callbacks failed after fault");
            }
        }
        finally
        {
            DisposeCts(cts);
            lock (_lock)
            {
                if (ReferenceEquals(_shutdownTask, timerTask))
                {
                    _shutdownTask = null;
                }
            }
        }
    }

    private void CancelCts(CancellationTokenSource cts, string failureMessage)
    {
        lock (_ctsLock)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                Log.Debug("[SchedulerService] Cancellation source was already disposed.");
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Warning(ex, failureMessage);
            }
        }
    }

    private void DisposeCts(CancellationTokenSource cts)
    {
        lock (_ctsLock)
        {
            cts.Dispose();
        }
    }

    private async Task CompleteStopAsync(Task timerTask, CancellationToken cancellationToken)
    {
        try
        {
            var completedTask = await Task.WhenAny(timerTask, Task.Delay(StopTimeout, _timeProvider, cancellationToken))
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!ReferenceEquals(completedTask, timerTask))
            {
                // Task.WhenAny returns the canceled Delay without throwing; surface the caller's
                // cancellation (shutdown continues in background).
                cancellationToken.ThrowIfCancellationRequested();
                Log.Warning("[SchedulerService] Timer loop did not stop within {TimeoutMs}ms; shutdown will continue in background", StopTimeout.TotalMilliseconds);
            }

            if (ReferenceEquals(completedTask, timerTask))
            {
                await timerTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Debug("[SchedulerService] Stop completed through cancellation.");
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            // Faults are already handled by ObserveTimerLoopAsync.
        }
    }

    private async Task CheckTasksAsync(CancellationToken cancellationToken)
    {
        ScheduledTask[] tasksToRun;
        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            tasksToRun = Tasks
                .Where(t => t.IsEnabled && t.NextRunTime is not null && t.NextRunTime.Value <= now)
                .ToArray();

            // Clear NextRunTime immediately to prevent duplicate triggers
            // It will be recalculated after execution for interval tasks
            foreach (var task in tasksToRun)
            {
                task.NextRunTime = null;
            }
        }

        foreach (var task in tasksToRun)
        {
            await _executor.ExecuteAsync(task, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SaveAsync()
    {
        // Snapshot to avoid locking during async I/O
        ScheduledTask[] tasksToSave;
        lock (_lock)
        {
            tasksToSave = Tasks.ToArray();
        }

        await _repository.SaveAsync(tasksToSave).ConfigureAwait(false);
    }

    public async Task LoadAsync()
    {
        var tasks = await _repository.LoadAsync().ConfigureAwait(false);

        await ExecuteOnCapturedContextAsync(() =>
        {
            lock (_lock)
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                Tasks.Clear();
                foreach (var task in tasks)
                {
                    if (task is null)
                    {
                        Log.Warning("[SchedulerService] Skipping null task entry during load");
                        continue;
                    }

                    try
                    {
                        task.Normalize();
                        if (!task.IsEnabled)
                        {
                            task.NextRunTime = null;
                        }
                        else if (task.Type is ScheduleType.Interval)
                        {
                            task.CalculateNextRunTime(now);
                        }
                        else if (task.Type is ScheduleType.SpecificTime)
                        {
                            if (task.ScheduledDateTime is null)
                            {
                                task.IsEnabled = false;
                                task.NextRunTime = null;
                                Log.Warning(
                                    "[SchedulerService] Task {TaskId} disabled during load because SpecificTime schedule has no ScheduledDateTime",
                                    task.Id);
                                Tasks.Add(task);
                                continue;
                            }

                            // Always recompute from ScheduledDateTime, ignoring persisted NextRunTime.
                            task.CalculateNextRunTime(now);
                            if (task.NextRunTime is null || task.NextRunTime.Value < now)
                            {
                                task.IsEnabled = false;
                                task.NextRunTime = null;
                            }
                        }
                        else if (task.Type is ScheduleType.Weekly)
                        {
                            if (task.WeeklyDays is ScheduleDays.None)
                            {
                                task.IsEnabled = false;
                                task.NextRunTime = null;
                                Log.Warning(
                                    "[SchedulerService] Task {TaskId} disabled during load because Weekly schedule has no selected days",
                                    task.Id);
                            }
                            else
                            {
                                task.CalculateNextRunTime(now);
                            }
                        }
                        else
                        {
                            task.NextRunTime = null;
                        }
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        task.IsEnabled = false;
                        task.NextRunTime = null;
                        Log.Warning(ex,
                            "[SchedulerService] Task {TaskId} disabled during load due to invalid schedule data",
                            task.Id);
                    }

                    Tasks.Add(task);
                }
            }
        }).ConfigureAwait(false);
    }

    private Task ExecuteOnCapturedContextAsync(Action action)
    {
        if (_syncContext is null || SynchronizationContext.Current == _syncContext)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _syncContext.Post(_ =>
        {
            try
            {
                action();
                if (!completion.TrySetResult(null))
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

        return completion.Task;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _executor.TaskExecuted -= OnExecutorTaskExecuted;
        _executor.TaskStarting -= OnExecutorTaskStarting;

        StopScheduler();
    }
}
