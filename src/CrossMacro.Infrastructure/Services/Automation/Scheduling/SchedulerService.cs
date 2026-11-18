using CrossMacro.Core.Services.Automation;

namespace CrossMacro.Infrastructure.Services.Automation.Scheduling;

/// <summary>
/// Service for scheduling and executing macro tasks
/// </summary>
public sealed class SchedulerService : ISchedulerService, IScheduledTaskOperations, IAutomationTaskStore<ScheduledTask>
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly IScheduledTaskExecutor _executor;
    private readonly TimeProvider _timeProvider;
    private readonly AutomationTaskStore<ScheduledTask> _store;
    private readonly Lock _lock;
    private readonly Lock _ctsLock = new();

    private PeriodicTimer? _periodicTimer;
    private CancellationTokenSource? _cts;
    private Task? _timerTask;
    private Task? _shutdownTask;
    private bool _disposed;

    public ObservableCollection<ScheduledTask> Tasks => _store.Tasks;

    public bool IsCurrentTask(ScheduledTask task) => _store.IsCurrent(task);

    IReadOnlyList<ScheduledTask> IAutomationTaskStore<ScheduledTask>.Tasks => _store.Snapshot();

    public bool IsRunning { get; private set; }

    public Task Completion
    {
        get
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

    public SchedulerService(
        IScheduledTaskRepository repository,
        IScheduledTaskExecutor executor,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _executor = executor;
        _timeProvider = timeProvider;
        _store = new AutomationTaskStore<ScheduledTask>(new AutomationTaskStoreOptions<ScheduledTask>
        {
            Name = "scheduled",
            GetId = task => task.Id,
            Copy = AutomationTaskSnapshots.Copy,
            HasSameConfiguration = AutomationTaskSnapshots.HasSameConfiguration,
            ApplyConfiguration = AutomationTaskSnapshots.ApplyConfiguration,
            Load = async _ => await repository.LoadAsync().ConfigureAwait(false),
            Save = (tasks, cancellationToken) => repository.SaveAsync(tasks, cancellationToken),
            PrepareCommit = PrepareCommit,
            PrepareLoaded = PrepareLoadedTask,
        });
        _lock = _store.Gate;

        _executor.TaskExecuted += OnExecutorTaskExecuted;
        _executor.TaskStarting += OnExecutorTaskStarting;
    }

    private void OnExecutorTaskExecuted(object? sender, TaskExecutedEventArgs e)
    {
        TaskExecuted.RaiseSafely(this, e, nameof(SchedulerService));
    }

    private void OnExecutorTaskStarting(object? sender, ScheduledTaskStartingEventArgs e)
    {
        TaskStarting.RaiseSafely(this, e, nameof(SchedulerService));
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
            _periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1), _timeProvider);
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

    public Task CommitAsync(IReadOnlyList<ScheduledTask> tasks, CancellationToken cancellationToken = default) =>
        _store.CommitAsync(tasks, cancellationToken);

    public Task SaveAsync() => _store.SaveAsync();

    Task IAutomationTaskStore<ScheduledTask>.LoadAsync() => _store.EnsureLoadedAsync();

    public Task LoadAsync() => _store.LoadAsync();

    private void PrepareCommit(ScheduledTask task, ScheduledTask? current)
    {
        task.Normalize();
        if (current is not null && AutomationTaskSnapshots.HasSameSchedule(task, current))
        {
            task.NextRunTime = current.NextRunTime;
        }
        else if (task.IsEnabled)
        {
            task.CalculateNextRunTime(_timeProvider.GetUtcNow().UtcDateTime);
        }
    }

    private void PrepareLoadedTask(ScheduledTask task)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
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
                    return;
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
        _store.Dispose();
    }
}
