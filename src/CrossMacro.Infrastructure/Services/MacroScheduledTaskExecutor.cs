using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

public class MacroScheduledTaskExecutor : IScheduledTaskExecutor
{
    private static readonly ILogger Logger = Log.ForContext<MacroScheduledTaskExecutor>();
    
    private readonly IMacroFileManager _fileManager;
    private readonly Func<IMacroPlayer> _playerFactory;
    private readonly ITimeProvider _timeProvider;
    private readonly SynchronizationContext? _syncContext;

    public event EventHandler<TaskExecutedEventArgs>? TaskExecuted;
    public event EventHandler<ScheduledTask>? TaskStarting;

    public MacroScheduledTaskExecutor(
        IMacroFileManager fileManager,
        Func<IMacroPlayer> playerFactory,
        ITimeProvider timeProvider)
    {
        _fileManager = fileManager;
        _playerFactory = playerFactory;
        _timeProvider = timeProvider;
        _syncContext = SynchronizationContext.Current;
    }

    public async Task ExecuteAsync(ScheduledTask task)
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
                SpeedMultiplier = PlaybackOptions.NormalizeSpeedMultiplier(task.PlaybackSpeed)
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
}
