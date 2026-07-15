
namespace CrossMacro.Infrastructure.Services;

public class MacroScheduledTaskExecutor : IScheduledTaskExecutor
{
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

    public async Task ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(task.MacroFilePath) || !File.Exists(task.MacroFilePath))
            {
                await SafeUpdateAsync(() =>
                {
                    task.LastStatus = "Macro file not found";
                    task.LastRunTime = _timeProvider.UtcNow;
                    UpdateScheduleAfterAttempt(task);
                });

                RaiseTaskExecuted(new TaskExecutedEventArgs(task, success: false, "Macro file not found"));
                return;
            }

            var macro = await _fileManager.LoadAsync(task.MacroFilePath);
            if (macro is null)
            {
                await SafeUpdateAsync(() =>
                {
                    task.LastStatus = "Failed to load macro";
                    task.LastRunTime = _timeProvider.UtcNow;
                    UpdateScheduleAfterAttempt(task);
                });
                RaiseTaskExecuted(new TaskExecutedEventArgs(task, success: false, "Failed to load macro"));
                return;
            }

            // Update status immediately before execution starts
            await SafeUpdateAsync(() =>
            {
                task.LastStatus = "Running...";
                task.LastRunTime = _timeProvider.UtcNow;
            });
            RaiseTaskStarting(task);

            // Create new player instance for this execution to avoid conflicts
            using var player = _playerFactory();

            // Apply task-specific playback speed
            var options = new PlaybackOptions
            {
                SpeedMultiplier = PlaybackOptions.NormalizeSpeedMultiplier(task.PlaybackSpeed),
            };

            await player.PlayAsync(macro, options, cancellationToken);

            // Update status after successful completion
            await SafeUpdateAsync(() =>
            {
                task.LastRunTime = _timeProvider.UtcNow;
                task.LastStatus = "Success";
                UpdateScheduleAfterAttempt(task);
            });

            RaiseTaskExecuted(new TaskExecutedEventArgs(task, success: true, "Executed successfully"));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("progress", StringComparison.Ordinal))
        {
            // Playback already in progress - reschedule for next interval
            await SafeUpdateAsync(() =>
            {
                task.LastStatus = "Skipped (playback busy)";
                UpdateScheduleAfterAttempt(task);
            });
            RaiseTaskExecuted(new TaskExecutedEventArgs(task, success: false, "Playback was busy, will retry"));
        }
        catch (OperationCanceledException)
        {
            await SafeUpdateAsync(() =>
            {
                task.LastStatus = "Cancelled";
                task.LastRunTime = _timeProvider.UtcNow;
                UpdateScheduleAfterAttempt(task);
            });
            RaiseTaskExecuted(new TaskExecutedEventArgs(task, success: false, "Cancelled"));
        }
        catch (Exception ex)
        {
            await SafeUpdateAsync(() =>
            {
                task.LastStatus = $"Error: {ex.Message}";
                task.LastRunTime = _timeProvider.UtcNow;
                UpdateScheduleAfterAttempt(task);
            });
            RaiseTaskExecuted(new TaskExecutedEventArgs(task, success: false, ex.Message));
        }
    }

    private void UpdateScheduleAfterAttempt(ScheduledTask task)
    {
        if (task.Type is ScheduleType.Interval or ScheduleType.Weekly)
        {
            task.CalculateNextRunTime(_timeProvider.UtcNow);
            return;
        }

        if (task.Type is ScheduleType.SpecificTime)
        {
            task.IsEnabled = false;
            task.NextRunTime = null;
        }
    }

    private Task SafeUpdateAsync(Action action)
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
                completion.TrySetResult(null);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }, state: null);
        return completion.Task;
    }

    private void RaiseTaskStarting(ScheduledTask task)
    {
        try { TaskStarting?.Invoke(this, task); }
        catch (Exception ex) { Log.Warning(ex, "[MacroScheduledTaskExecutor] TaskStarting subscriber threw"); }
    }

    private void RaiseTaskExecuted(TaskExecutedEventArgs args)
    {
        try { TaskExecuted?.Invoke(this, args); }
        catch (Exception ex) { Log.Warning(ex, "[MacroScheduledTaskExecutor] TaskExecuted subscriber threw"); }
    }
}
