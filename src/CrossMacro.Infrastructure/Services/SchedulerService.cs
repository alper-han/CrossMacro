using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Serialization;
using Serilog;
using CrossMacro.Core;
using CrossMacro.Infrastructure.Helpers;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for scheduling and executing macro tasks
/// </summary>
public class SchedulerService : ISchedulerService
{
    private static readonly ILogger Logger = Log.ForContext<SchedulerService>();
    
    private readonly IMacroFileManager _fileManager;
    private readonly Func<IMacroPlayer> _playerFactory;
    private readonly ITimeProvider _timeProvider;
    private readonly SynchronizationContext? _syncContext;
    private readonly Lock _lock = new();
    private PeriodicTimer? _periodicTimer;
    private CancellationTokenSource? _cts;
    private Task? _timerTask;
    private bool _isRunning;
    private bool _disposed;
    
    private readonly string _scheduleFilePath;
    
    public ObservableCollection<ScheduledTask> Tasks { get; } = new();
    public bool IsRunning => _isRunning;
    
    public event EventHandler<TaskExecutedEventArgs>? TaskExecuted;
    public event EventHandler<ScheduledTask>? TaskStarting;
    
    public SchedulerService(IMacroFileManager fileManager, Func<IMacroPlayer> playerFactory, ITimeProvider timeProvider)
    {
        _fileManager = fileManager;
        _playerFactory = playerFactory;
        _timeProvider = timeProvider;
        // Capture context for safe UI updates
        _syncContext = SynchronizationContext.Current;

        // Follow XDG Base Directory specification
        _scheduleFilePath = PathHelper.GetConfigFilePath("schedules.json");
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
            await ExecuteTaskAsync(task);
        }
    }
    
    private async Task ExecuteTaskAsync(ScheduledTask task)
    {
        try
        {
            if (string.IsNullOrEmpty(task.MacroFilePath) || !File.Exists(task.MacroFilePath))
            {
                SafeUpdate(() => 
                {
                    task.LastStatus = "Macro file not found";
                    task.LastRunTime = _timeProvider.UtcNow;
                    
                    // Disable one-time tasks that failed
                    if (task.Type == ScheduleType.SpecificTime)
                    {
                        task.IsEnabled = false;
                        task.NextRunTime = null;
                    }
                });
                
                TaskExecuted?.Invoke(this, new TaskExecutedEventArgs(task, false, "Macro file not found"));
                return;
            }
            
            var macro = await _fileManager.LoadAsync(task.MacroFilePath);
            if (macro == null)
            {
                SafeUpdate(() =>
                {
                    task.LastStatus = "Failed to load macro";
                    task.LastRunTime = _timeProvider.UtcNow;
                });
                TaskExecuted?.Invoke(this, new TaskExecutedEventArgs(task, false, "Failed to load macro"));
                return;
            }
            
            // Update status immediately before execution starts
            SafeUpdate(() =>
            {
                task.LastStatus = "Running...";
                task.LastRunTime = _timeProvider.UtcNow;
            });
            TaskStarting?.Invoke(this, task);
            
            // Create new player instance for this execution to avoid conflicts
            using var player = _playerFactory();
            
            // Apply task-specific playback speed
            var options = new PlaybackOptions
            {
                SpeedMultiplier = task.PlaybackSpeed
            };
            
            await player.PlayAsync(macro, options);
            
            // Update status after successful completion
            SafeUpdate(() =>
            {
                task.LastRunTime = _timeProvider.UtcNow;
                task.LastStatus = "Success";
                
                // Calculate next run time for interval tasks
                if (task.Type == ScheduleType.Interval)
                {
                    task.CalculateNextRunTime(_timeProvider.UtcNow);
                }
                else if (task.Type == ScheduleType.SpecificTime)
                {
                    // One-time task completed, disable it
                    task.IsEnabled = false;
                    task.NextRunTime = null;
                }
            });
            
            TaskExecuted?.Invoke(this, new TaskExecutedEventArgs(task, true, "Executed successfully"));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("progress"))
        {
            // Playback already in progress - reschedule for next interval
            SafeUpdate(() =>
            {
                task.LastStatus = "Skipped (playback busy)";
                if (task.Type == ScheduleType.Interval)
                {
                    task.CalculateNextRunTime(_timeProvider.UtcNow);
                }
            });
            TaskExecuted?.Invoke(this, new TaskExecutedEventArgs(task, false, "Playback was busy, will retry"));
        }
        catch (Exception ex)
        {
            SafeUpdate(() =>
            {
                task.LastStatus = $"Error: {ex.Message}";
                task.LastRunTime = _timeProvider.UtcNow;
            });
            TaskExecuted?.Invoke(this, new TaskExecutedEventArgs(task, false, ex.Message));
        }
    }

    private void SafeUpdate(Action action)
    {
        if (_syncContext != null)
        {
            _syncContext.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }
    
    public async Task SaveAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_scheduleFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var json = JsonSerializer.Serialize(Tasks.ToList(), CrossMacroJsonContext.Default.ListScheduledTask);
            await File.WriteAllTextAsync(_scheduleFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to save scheduled tasks to {Path}", _scheduleFilePath);
        }
    }
    
    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_scheduleFilePath)) return;
            
            var json = await File.ReadAllTextAsync(_scheduleFilePath);
            var tasks = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.ListScheduledTask);
            
            if (tasks != null)
            {
                void UpdateCollection(object? state)
                {
                    lock (_lock)
                    {
                        Tasks.Clear();
                        foreach (var task in tasks)
                        {
                            // Recalculate next run time for interval tasks
                            if (task.IsEnabled && task.Type == ScheduleType.Interval)
                            {
                                task.CalculateNextRunTime(_timeProvider.UtcNow);
                            }
                            // Check if SpecificTime tasks are still valid
                            else if (task.Type == ScheduleType.SpecificTime) 
                            { 
                                // Ensure UTC comparison
                                var scheduledUtc = task.ScheduledDateTime?.ToUniversalTime() ?? DateTime.MinValue;
 
                                if (scheduledUtc < _timeProvider.UtcNow) 
                                { 
                                    task.IsEnabled = false; 
                                    task.NextRunTime = null; 
                                } 
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
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to load scheduled tasks from {Path}", _scheduleFilePath);
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Stop();
    }
}
