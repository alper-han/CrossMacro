
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class SchedulerServiceTests
{
    private readonly IScheduledTaskRepository _repository;
    private readonly IScheduledTaskExecutor _executor;
    private readonly TimeProvider _timeProvider;
    private readonly SchedulerService _service;

    public SchedulerServiceTests()
    {
        _repository = Substitute.For<IScheduledTaskRepository>();
        _executor = Substitute.For<IScheduledTaskExecutor>();
        _timeProvider = Substitute.For<TimeProvider>();

        // Default time
        _ = _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

        _service = new SchedulerService(_repository, _executor, _timeProvider);
    }

    [Fact]
    public void Start_SetsIsRunningToTrue()
    {
        _service.Start();
        _ = _service.IsRunning.Should().BeTrue();
        _service.StopScheduler();
    }

    [Fact]
    public void Stop_SetsIsRunningToFalse()
    {
        _service.Start();
        _service.StopScheduler();
        _ = _service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Stop_ExposesCompletionOfSchedulerLifetime()
    {
        _service.Start();
        _service.StopScheduler();

        await _service.Completion.WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);

        _ = _service.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_CompletesAfterNormalTimerShutdown()
    {
        _service.Start();

        await _service.StopAsync().WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);

        _ = _service.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Start_AfterNormalStopCanRestartImmediately()
    {
        _service.Start();
        await _service.StopAsync().WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);

        _service.Start();

        _ = _service.IsRunning.Should().BeTrue();
        await _service.StopAsync().WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);
    }

    [Fact]
    public async Task StopScheduler_ReturnsWithoutWaitingAndExposesCompletion()
    {
        var executionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowExecutionToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _executor.ExecuteAsync(Arg.Any<ScheduledTask>(), Arg.Any<CancellationToken>())
            .Returns(unusedCallInfo =>
            {
                _ = executionStarted.TrySetResult();
                return allowExecutionToFinish.Task;
            });

        var task = new ScheduledTask
        {
            Name = "Run now",
            MacroFilePath = "test.macro",
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Seconds,
            IntervalValue = 30,
            IsEnabled = false,
        };
        _service.AddTask(task);
        task.IsEnabled = true;
        task.NextRunTime = _timeProvider.GetUtcNow().UtcDateTime;
        _service.Start();

        await executionStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), _timeProvider);

        _service.StopScheduler();
        var secondStop = _service.StopAsync();

        _ = _service.IsRunning.Should().BeFalse();
        _ = _service.Completion.IsCompleted.Should().BeFalse();
        _ = secondStop.IsCompleted.Should().BeFalse();

        _ = allowExecutionToFinish.TrySetResult();
        await secondStop.WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);
        await _service.Completion.WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);
    }

    [Fact]
    public async Task StopAsync_CallerCancellationDoesNotCancelTimerShutdown()
    {
        var executionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowExecutionToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executionCancellationToken = CancellationToken.None;
        _ = _executor.ExecuteAsync(Arg.Any<ScheduledTask>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _ = executionStarted.TrySetResult();
                executionCancellationToken = callInfo.Arg<CancellationToken>();
                return allowExecutionToFinish.Task;
            });

        var task = new ScheduledTask
        {
            Name = "Run now",
            MacroFilePath = "test.macro",
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Seconds,
            IntervalValue = 30,
            IsEnabled = false,
        };
        _service.AddTask(task);
        task.IsEnabled = true;
        task.NextRunTime = _timeProvider.GetUtcNow().UtcDateTime;
        _service.Start();
        await executionStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), _timeProvider);

        using var callerCancellation = new CancellationTokenSource();
        callerCancellation.Cancel();

        var stopTask = _service.StopAsync(callerCancellation.Token);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await stopTask.WaitAsync(TimeSpan.FromSeconds(2), _timeProvider));

        _ = _service.Completion.IsCompleted.Should().BeFalse();
        using var registration = executionCancellationToken.Register(static () => { });
        _ = allowExecutionToFinish.TrySetResult();
        await _service.Completion.WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);
    }

    [Fact]
    public async Task StopAsync_TimeoutLeavesCtsOwnedUntilTimerCompletes()
    {
        var service = new SchedulerService(_repository, _executor, TimeProvider.System);
        var executionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowExecutionToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executionCancellationToken = CancellationToken.None;
        _ = _executor.ExecuteAsync(Arg.Any<ScheduledTask>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _ = executionStarted.TrySetResult();
                executionCancellationToken = callInfo.Arg<CancellationToken>();
                return allowExecutionToFinish.Task;
            });

        var task = new ScheduledTask
        {
            Name = "Run now",
            MacroFilePath = "test.macro",
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Seconds,
            IntervalValue = 30,
            IsEnabled = false,
        };
        service.AddTask(task);
        task.IsEnabled = true;
        task.NextRunTime = DateTime.UtcNow;
        service.Start();
        await executionStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await service.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));

        _ = service.Completion.IsCompleted.Should().BeFalse();
        service.Start();
        _ = service.IsRunning.Should().BeFalse();
        using var registration = executionCancellationToken.Register(static () => { });
        _ = allowExecutionToFinish.TrySetResult();
        await service.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        service.Start();
        _ = service.IsRunning.Should().BeTrue();
        await service.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task StopAndDispose_AreIdempotent()
    {
        _service.Start();

        await _service.StopAsync().WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);
        await _service.StopAsync().WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);

        _service.Dispose();
        _service.Dispose();

        await _service.Completion.WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);
        _ = _service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_DoesNotExecuteTasksAfterShutdown()
    {
        var task = new ScheduledTask
        {
            Name = "Future task",
            MacroFilePath = "test.macro",
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Seconds,
            IntervalValue = 30,
            IsEnabled = true,
            NextRunTime = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(1),
        };
        _service.AddTask(task);
        _service.Start();

        await _service.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        _ = _executor.DidNotReceive().ExecuteAsync(Arg.Any<ScheduledTask>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AddTask_AddsToCollection()
    {
        var task = new ScheduledTask();
        _service.AddTask(task);
        _ = _service.Tasks.Should().Contain(task);
    }

    [Fact]
    public void RemoveTask_RemovesFromCollection()
    {
        var task = new ScheduledTask();
        _service.AddTask(task);
        _service.RemoveTask(task.Id);
        _ = _service.Tasks.Should().NotContain(task);
    }

    [Fact]
    public void SetTaskEnabled_WhenTrue_CalculatesNextRunTime()
    {
        // Arrange
        var task = new ScheduledTask { MacroFilePath = "test.macro", Type = ScheduleType.Interval, IntervalValue = 60, IntervalUnit = IntervalUnit.Seconds };
        _service.AddTask(task);

        // Act
        _service.SetTaskEnabled(task.Id, enabled: true);

        // Assert
        var t = _service.Tasks.First(x => x.Id == task.Id);
        _ = t.IsEnabled.Should().BeTrue();
        _ = t.NextRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddSeconds(60));
    }

    [Fact]
    public void SetTaskEnabled_WhenFalse_ClearsNextRunTime()
    {
        // Arrange
        var task = new ScheduledTask { MacroFilePath = "test.macro", IsEnabled = true };
        _service.AddTask(task); // Adds and calculates

        // Act
        _service.SetTaskEnabled(task.Id, enabled: false);

        // Assert
        var t = _service.Tasks.First(x => x.Id == task.Id);
        _ = t.IsEnabled.Should().BeFalse();
        _ = t.NextRunTime.Should().BeNull();
    }

    [Fact]
    public void UpdateTask_UpdatesExistingTaskProperties()
    {
        // Arrange
        var original = new ScheduledTask { Name = "Old Name", MacroFilePath = "old.macro" };
        _service.AddTask(original);

        var updated = new ScheduledTask
        {
            Id = original.Id,
            Name = "New Name",
            MacroFilePath = "new.macro",
        };

        // Act
        _service.UpdateTask(updated);

        // Assert
        var t = _service.Tasks.First(x => x.Id == original.Id);
        _ = t.Name.Should().Be("New Name");
        _ = t.MacroFilePath.Should().Be("new.macro");
    }

    [Fact]
    public async Task LoadAsync_SpecificTimeEnabledFuture_RecalculatesNextRunTimeFromScheduledDateTime()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var scheduledUtc = now.AddHours(3);
        var task = new ScheduledTask
        {
            Name = "Future task",
            MacroFilePath = "task.macro",
            Type = ScheduleType.SpecificTime,
            ScheduledDateTime = scheduledUtc,
            IsEnabled = true,
            NextRunTime = now.AddMinutes(5), // stale persisted value
        };

        _ = _repository.LoadAsync().Returns(Task.FromResult<IReadOnlyList<ScheduledTask>>(new List<ScheduledTask> { task }));

        // Act
        await _service.LoadAsync();

        // Assert
        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        _ = loaded.IsEnabled.Should().BeTrue();
        _ = loaded.NextRunTime.Should().Be(scheduledUtc);
    }

    [Fact]
    public async Task LoadAsync_SpecificTimePast_DisablesTaskAndClearsNextRunTime()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var task = new ScheduledTask
        {
            Name = "Past task",
            MacroFilePath = "task.macro",
            Type = ScheduleType.SpecificTime,
            ScheduledDateTime = now.AddMinutes(-10),
            IsEnabled = true,
            NextRunTime = now.AddHours(10), // stale persisted value
        };

        _ = _repository.LoadAsync().Returns(Task.FromResult<IReadOnlyList<ScheduledTask>>(new List<ScheduledTask> { task }));

        // Act
        await _service.LoadAsync();

        // Assert
        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        _ = loaded.IsEnabled.Should().BeFalse();
        _ = loaded.NextRunTime.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_HugeIntervalTask_DoesNotThrow_AndClampsNextRunTime()
    {
        // Arrange
        var task = new ScheduledTask
        {
            Name = "Huge interval",
            MacroFilePath = "task.macro",
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Hours,
            IntervalValue = int.MaxValue,
            IsEnabled = true,
        };

        _ = _repository.LoadAsync().Returns(Task.FromResult<IReadOnlyList<ScheduledTask>>(new List<ScheduledTask> { task }));

        // Act
        Func<Task> act = async () => await _service.LoadAsync();

        // Assert
        _ = await act.Should().NotThrowAsync();
        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        _ = loaded.IsEnabled.Should().BeTrue();
        _ = loaded.NextRunTime.Should().NotBeNull();
        _ = loaded.NextRunTime!.Value.Ticks.Should().Be(DateTime.MaxValue.Ticks);
    }

    [Fact]
    public async Task LoadAsync_SpecificTimeWithoutScheduledDate_DisablesTaskAndClearsNextRunTime()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var task = new ScheduledTask
        {
            Name = "Invalid specific-time",
            MacroFilePath = "task.macro",
            Type = ScheduleType.SpecificTime,
            ScheduledDateTime = null,
            IsEnabled = true,
            NextRunTime = now.AddHours(5), // stale persisted value
        };

        _ = _repository.LoadAsync().Returns(Task.FromResult<IReadOnlyList<ScheduledTask>>(new List<ScheduledTask> { task }));

        // Act
        await _service.LoadAsync();

        // Assert
        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        _ = loaded.IsEnabled.Should().BeFalse();
        _ = loaded.NextRunTime.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WeeklyEnabled_RecalculatesNextRunTime()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var task = new ScheduledTask
        {
            Name = "Weekly task",
            MacroFilePath = "task.macro",
            Type = ScheduleType.Weekly,
            WeeklyDays = ScheduleDays.EveryDay,
            WeeklyTime = now.ToLocalTime().TimeOfDay.Add(TimeSpan.FromMinutes(30)),
            IsEnabled = true,
            NextRunTime = now.AddDays(3),
        };

        _ = _repository.LoadAsync().Returns(Task.FromResult<IReadOnlyList<ScheduledTask>>(new List<ScheduledTask> { task }));

        await _service.LoadAsync();

        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        _ = loaded.IsEnabled.Should().BeTrue();
        _ = loaded.NextRunTime.Should().NotBeNull();
        _ = loaded.NextRunTime!.Value.Should().BeOnOrAfter(now);
        _ = loaded.NextRunTime.Value.Should().BeBefore(now.AddDays(1));
    }

    [Fact]
    public async Task LoadAsync_WeeklyWithoutDays_DisablesTaskAndClearsNextRunTime()
    {
        var task = new ScheduledTask
        {
            Name = "Invalid weekly task",
            MacroFilePath = "task.macro",
            Type = ScheduleType.Weekly,
            WeeklyDays = ScheduleDays.None,
            WeeklyTime = new TimeSpan(9, 0, 0),
            IsEnabled = true,
            NextRunTime = _timeProvider.GetUtcNow().UtcDateTime.AddHours(1),
        };

        _ = _repository.LoadAsync().Returns(Task.FromResult<IReadOnlyList<ScheduledTask>>(new List<ScheduledTask> { task }));

        await _service.LoadAsync();

        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        _ = loaded.IsEnabled.Should().BeFalse();
        _ = loaded.NextRunTime.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WhenRepositoryContainsNullTask_SkipsNullAndLoadsValidTasks()
    {
        // Arrange
        var validTask = new ScheduledTask
        {
            Name = "Valid",
            MacroFilePath = "task.macro",
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Seconds,
            IntervalValue = 30,
            IsEnabled = true,
        };

        _ = _repository.LoadAsync().Returns(Task.FromResult<IReadOnlyList<ScheduledTask>>(new List<ScheduledTask> { null!, validTask }));

        // Act
        await _service.LoadAsync();

        // Assert
        _ = _service.Tasks.Should().ContainSingle();
        _ = _service.Tasks[0].Id.Should().Be(validTask.Id);
    }

    [Fact]
    public async Task LoadAsync_WhenCalledOnCapturedSynchronizationContext_CompletesInlineAfterCollectionUpdate()
    {
        var previousContext = SynchronizationContext.Current;
        var synchronizationContext = new DeferredSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(synchronizationContext);

        try
        {
            var task = new ScheduledTask
            {
                Name = "Queued task",
                MacroFilePath = "task.macro",
                Type = ScheduleType.Interval,
                IntervalUnit = IntervalUnit.Seconds,
                IntervalValue = 30,
                IsEnabled = true,
            };

            _ = _repository.LoadAsync().Returns(Task.FromResult<IReadOnlyList<ScheduledTask>>(new List<ScheduledTask> { task }));
            var service = new SchedulerService(_repository, _executor, _timeProvider);

            var loadTask = service.LoadAsync();
            SynchronizationContext.SetSynchronizationContext(previousContext);

            await loadTask;

            _ = loadTask.IsCompletedSuccessfully.Should().BeTrue();
            _ = service.Tasks.Should().ContainSingle();
            _ = synchronizationContext.PendingCallbacks.Should().Be(0);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenCalledOffCapturedSynchronizationContext_PostsBackBeforeCompleting()
    {
        var previousContext = SynchronizationContext.Current;
        var synchronizationContext = new DeferredSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(synchronizationContext);

        try
        {
            var task = new ScheduledTask
            {
                Name = "Queued task",
                MacroFilePath = "task.macro",
                Type = ScheduleType.Interval,
                IntervalUnit = IntervalUnit.Seconds,
                IntervalValue = 30,
                IsEnabled = true,
            };

            _ = _repository.LoadAsync().Returns(Task.FromResult<IReadOnlyList<ScheduledTask>>(new List<ScheduledTask> { task }));
            var service = new SchedulerService(_repository, _executor, _timeProvider);
            SynchronizationContext.SetSynchronizationContext(previousContext);

            var workerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var workerMayLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var loadTask = Task.Run(async () =>
            {
                workerStarted.SetResult();
                await workerMayLoad.Task;
                await service.LoadAsync();
            });

            await workerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);
            workerMayLoad.SetResult();
            await synchronizationContext.PostObserved.Task.WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);

            _ = loadTask.IsCompleted.Should().BeFalse();
            _ = synchronizationContext.PendingCallbacks.Should().Be(1);
            _ = service.Tasks.Should().BeEmpty();

            synchronizationContext.RunAll();
            await loadTask.WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);

            _ = service.Tasks.Should().ContainSingle();
            _ = synchronizationContext.PendingCallbacks.Should().Be(0);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public async Task Stop_WhenTimerLoopIsBusyWithExecution_ReturnsWithoutWaitingForHungExecution()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var task = new ScheduledTask
        {
            Name = "Run now",
            MacroFilePath = "task.macro",
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Seconds,
            IntervalValue = 30,
            IsEnabled = false,
        };

        var executionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowExecutionToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _executor.ExecuteAsync(Arg.Any<ScheduledTask>(), Arg.Any<CancellationToken>())
            .Returns(unusedCallInfo =>
            {
                _ = executionStarted.TrySetResult();
                return allowExecutionToFinish.Task;
            });

        _service.AddTask(task);
        task.IsEnabled = true;
        task.NextRunTime = now;
        _service.Start();

        await executionStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), _timeProvider);

        var stopTask = Task.Run(_service.StopScheduler);
        await stopTask.WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);

        _ = _service.IsRunning.Should().BeFalse();
        _ = allowExecutionToFinish.TrySetResult();
        await _service.Completion.WaitAsync(TimeSpan.FromSeconds(2), _timeProvider);
    }

    private sealed class DeferredSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _pendingCallbacks = new();

        public TaskCompletionSource PostObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int PendingCallbacks
        {
            get
            {
                lock (_pendingCallbacks)
                {
                    return _pendingCallbacks.Count;
                }
            }
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_pendingCallbacks)
            {
                _pendingCallbacks.Enqueue((d, state));
            }

            _ = PostObserved.TrySetResult();
        }

        public void RunAll()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) workItem;

                lock (_pendingCallbacks)
                {
                    if (_pendingCallbacks.Count is 0)
                    {
                        return;
                    }

                    workItem = _pendingCallbacks.Dequeue();
                }

                workItem.Callback(workItem.State);
            }
        }
    }
}
