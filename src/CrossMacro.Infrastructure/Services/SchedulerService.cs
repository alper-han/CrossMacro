using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for scheduling and executing macro tasks
/// </summary>
public class SchedulerService : ISchedulerService
{
    private static readonly ILogger Logger = Log.ForContext<SchedulerService>();
    
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
                existing.ScheduledDateTime = task.ScheduledDateTime;
                
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
        if (_isRunning) return;
        _isRunning = true;
        
        _cts = new CancellationTokenSource();
        _periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _timerTask = RunTimerLoopAsync(_cts.Token);
    }
    
    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        
        _cts?.Cancel();
        _periodicTimer?.Dispose();
        _periodicTimer = null;
        
        // Wait for timer task to complete gracefully
        try
        {
            _timerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Task was cancelled, expected behavior
        }
        
        _timerTask = null;
        _cts?.Dispose();
        _cts = null;
    }
    
    private async Task RunTimerLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_periodicTimer != null && 
                   await _periodicTimer.WaitForNextTickAsync(cancellationToken))
            {
                await CheckTasksAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping the scheduler
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
        
        void UpdateCollection(object? state)
        {
            lock (_lock)
            {
                var now = _timeProvider.UtcNow;
                Tasks.Clear();
                foreach (var task in tasks)
                {
                    if (task == null)
                    {
                        Logger.Warning("[SchedulerService] Skipping null task entry during load");
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
                                Logger.Warning(
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
                        else
                        {
                            task.NextRunTime = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        task.IsEnabled = false;
                        task.NextRunTime = null;
                        Logger.Warning(ex,
                            "[SchedulerService] Task {TaskId} disabled during load due to invalid schedule data",
                            task.Id);
                    }
                    Tasks.Add(task);
                }
            }
        }

        if (_syncContext != null)
        {
            _syncContext.Post(UpdateCollection, null);
        }
        else
        {
            UpdateCollection(null);
        }
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
