using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for scheduling and executing macro tasks
/// </summary>
public class SchedulerService : ISchedulerService
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);
    
    private readonly IScheduledTaskRepository _repository;
    private readonly IScheduledTaskExecutor _executor;
    private readonly ITimeProvider _timeProvider;
    private readonly SynchronizationContext? _syncContext;
    private readonly Lock _lock = new();
    
    private PeriodicTimer? _periodicTimer;
    private CancellationTokenSource? _cts;
    private Task? _timerTask;
    private bool _isRunning;
    private bool _disposed;
    
    public ObservableCollection<ScheduledTask> Tasks { get; } = new();
    public bool IsRunning => _isRunning;
    
    public event EventHandler<TaskExecutedEventArgs>? TaskExecuted;
    public event EventHandler<ScheduledTask>? TaskStarting;
    
    public SchedulerService(
        IScheduledTaskRepository repository,
        IScheduledTaskExecutor executor,
        ITimeProvider timeProvider)
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
        TaskExecuted?.Invoke(this, e);
    }

    private void OnExecutorTaskStarting(object? sender, ScheduledTask e)
    {
        TaskStarting?.Invoke(this, e);
    }
    
    public void AddTask(ScheduledTask task)
    {
        lock (_lock)
        {
            Tasks.Add(task);
            if (task.IsEnabled)
            {
                task.CalculateNextRunTime(_timeProvider.UtcNow);
            }
        }
    }
    
    public void RemoveTask(Guid id)
    {
        lock (_lock)
        {
            var task = Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                Tasks.Remove(task);
            }
        }
    }
    
    public void UpdateTask(ScheduledTask task)
    {
        lock (_lock)
        {
            var existing = Tasks.FirstOrDefault(t => t.Id == task.Id);
            if (existing != null)
            {
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
                
                // Update IsEnabled last as it might trigger recalculations
                existing.IsEnabled = task.IsEnabled;
            }
        }
    }
    
    public void SetTaskEnabled(Guid id, bool enabled)
    {
        lock (_lock)
        {
            var task = Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                task.IsEnabled = enabled;
                if (enabled)
                {
                    task.CalculateNextRunTime(_timeProvider.UtcNow);
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
        
        if (task != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _executor.ExecuteAsync(task, cancellationToken);
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Task? timerTask = null;
        CancellationTokenSource? cts = null;

        lock (_lock)
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            _cts = new CancellationTokenSource();
            _periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            _timerTask = RunTimerLoopAsync(_periodicTimer, _cts.Token);

            timerTask = _timerTask;
            cts = _cts;
        }

        _ = ObserveTimerLoopAsync(timerTask!, cts!);
    }
    
    public void Stop()
    {
        Task? timerTask;
        PeriodicTimer? periodicTimer;
        CancellationTokenSource? cts;

        lock (_lock)
        {
            if (!_isRunning && _periodicTimer == null && _cts == null && _timerTask == null)
            {
                return;
            }

            _isRunning = false;
            timerTask = _timerTask;
            periodicTimer = _periodicTimer;
            cts = _cts;

            _timerTask = null;
            _periodicTimer = null;
            _cts = null;
        }

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        periodicTimer?.Dispose();

        if (timerTask == null)
        {
            cts?.Dispose();
            return;
        }

        _ = CompleteStopAsync(timerTask, cts);
    }
    
    private async Task RunTimerLoopAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await CheckTasksAsync();
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
        }
        catch (Exception ex)
        {
            bool shouldCleanup;
            lock (_lock)
            {
                shouldCleanup = ReferenceEquals(_timerTask, timerTask);
                if (shouldCleanup)
                {
                    _isRunning = false;
                    _timerTask = null;
                    _periodicTimer = null;
                    _cts = null;
                }
            }

            Log.Error(ex, "[SchedulerService] Timer loop faulted and scheduler was stopped");

            if (shouldCleanup)
            {
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                cts.Dispose();
            }
        }
    }

    private static async Task CompleteStopAsync(Task timerTask, CancellationTokenSource? cts)
    {
        try
        {
            var completedTask = await Task.WhenAny(timerTask, Task.Delay(StopTimeout)).ConfigureAwait(false);
            if (!ReferenceEquals(completedTask, timerTask))
            {
                Log.Warning("[SchedulerService] Timer loop did not stop within {TimeoutMs}ms; shutdown will continue in background", StopTimeout.TotalMilliseconds);
            }

            await timerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts?.IsCancellationRequested == true)
        {
        }
        catch
        {
            // Faults are already handled by ObserveTimerLoopAsync.
        }
        finally
        {
            cts?.Dispose();
        }
    }
    
    private async Task CheckTasksAsync()
    {
        ScheduledTask[] tasksToRun;
        lock (_lock)
        {
            var now = _timeProvider.UtcNow;
            tasksToRun = Tasks
                .Where(t => t.IsEnabled && t.NextRunTime.HasValue && t.NextRunTime.Value <= now)
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
            await _executor.ExecuteAsync(task);
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
        
        await _repository.SaveAsync(tasksToSave);
    }
    
    public async Task LoadAsync()
    {
        var tasks = await _repository.LoadAsync();

        await ExecuteOnCapturedContextAsync(() =>
        {
            lock (_lock)
            {
                var now = _timeProvider.UtcNow;
                Tasks.Clear();
                foreach (var task in tasks)
                {
                    if (task == null)
                    {
                        Log.Warning("[SchedulerService] Skipping null task entry during load");
                        continue;
                    }

                    try
                    {
                        if (!task.IsEnabled)
                        {
                            task.NextRunTime = null;
                        }
                        else if (task.Type == ScheduleType.Interval)
                        {
                            task.CalculateNextRunTime(now);
                        }
                        else if (task.Type == ScheduleType.SpecificTime)
                        {
                            if (!task.ScheduledDateTime.HasValue)
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
                            if (!task.NextRunTime.HasValue || task.NextRunTime.Value < now)
                            {
                                task.IsEnabled = false;
                                task.NextRunTime = null;
                            }
                        }
                        else if (task.Type == ScheduleType.Weekly)
                        {
                            if (task.WeeklyDays == ScheduleDays.None)
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
                    catch (Exception ex)
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
        if (_syncContext == null || SynchronizationContext.Current == _syncContext)
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
                completion.TrySetResult(null);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }, null);

        return completion.Task;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _executor.TaskExecuted -= OnExecutorTaskExecuted;
        _executor.TaskStarting -= OnExecutorTaskStarting;
        
        Stop();
    }
}
