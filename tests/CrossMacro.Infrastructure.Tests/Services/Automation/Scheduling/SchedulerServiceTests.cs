
namespace CrossMacro.Infrastructure.Tests.Services.Automation.Scheduling;

public sealed class SchedulerServiceTests : IDisposable
{
    private readonly IScheduledTaskRepository _repository;
    private readonly IScheduledTaskExecutor _executor;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly SchedulerService _service;

    public SchedulerServiceTests()
    {
        _repository = Substitute.For<IScheduledTaskRepository>();
        _executor = Substitute.For<IScheduledTaskExecutor>();
        _service = new SchedulerService(_repository, _executor, _clock);
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

        await _service.Completion.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);

        _ = _service.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_CompletesAfterNormalTimerShutdown()
    {
        _service.Start();

        await _service.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);

        _ = _service.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Start_AfterNormalStopCanRestartImmediately()
    {
        _service.Start();
        await _service.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);

        _service.Start();

        _ = _service.IsRunning.Should().BeTrue();
        await _service.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
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

        _ = CreateRunNowTask(_clock);
        _service.Start();
        _clock.Advance(TimeSpan.FromSeconds(1));

        await executionStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, TestContext.Current.CancellationToken);

        _service.StopScheduler();
        var secondStop = _service.StopAsync(TestContext.Current.CancellationToken);

        _ = _service.IsRunning.Should().BeFalse();
        _ = _service.Completion.IsCompleted.Should().BeFalse();
        _ = secondStop.IsCompleted.Should().BeFalse();

        _ = allowExecutionToFinish.TrySetResult();
        await secondStop.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
        await _service.Completion.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
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

        _ = CreateRunNowTask(_clock);
        _service.Start();
        _clock.Advance(TimeSpan.FromSeconds(1));
        await executionStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, TestContext.Current.CancellationToken);

        using var callerCancellation = new CancellationTokenSource();
        await callerCancellation.CancelAsync();

        var stopTask = _service.StopAsync(callerCancellation.Token);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await stopTask.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken));

        _ = _service.Completion.IsCompleted.Should().BeFalse();
        using var registration = executionCancellationToken.Register(static () => { });
        _ = allowExecutionToFinish.TrySetResult();
        await _service.Completion.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StopAsync_TimeoutLeavesCtsOwnedUntilTimerCompletes()
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

        _ = CreateRunNowTask(_clock);
        _service.Start();
        _clock.Advance(TimeSpan.FromSeconds(1));
        await executionStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, TestContext.Current.CancellationToken);

        var stopTask = _service.StopAsync(TestContext.Current.CancellationToken);
        _ = stopTask.IsCompleted.Should().BeFalse();
        _clock.Advance(TimeSpan.FromSeconds(2));
        await stopTask.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, TestContext.Current.CancellationToken);

        _ = _service.Completion.IsCompleted.Should().BeFalse();
        _service.Start();
        _ = _service.IsRunning.Should().BeFalse();
        using var registration = executionCancellationToken.Register(static () => { });
        _ = allowExecutionToFinish.TrySetResult();
        await _service.Completion.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
        _service.Start();
        _ = _service.IsRunning.Should().BeTrue();
        await _service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StopAndDispose_AreIdempotent()
    {
        _service.Start();

        await _service.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
        await _service.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);

        _service.Dispose();
        _service.Dispose();

        await _service.Completion.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
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
            NextRunTime = _clock.GetUtcNow().UtcDateTime.AddMinutes(1),
        };
        _service.Tasks.Add(task);
        _service.Start();

        await _service.StopAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);

        _ = _executor.DidNotReceive().ExecuteAsync(Arg.Any<ScheduledTask>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitAsync_WhenTaskIsEnabled_CalculatesNextRunTime()
    {
        var draft = new ScheduledTask { MacroFilePath = "test.macro", Type = ScheduleType.Interval, IntervalValue = 60, IntervalUnit = IntervalUnit.Seconds, IsEnabled = true };

        await _service.CommitAsync([draft], TestContext.Current.CancellationToken);

        var committed = Assert.Single(_service.Tasks);
        _ = committed.NextRunTime.Should().Be(_clock.GetUtcNow().UtcDateTime.AddSeconds(60));
    }

    [Fact]
    public async Task CommitAsync_WhenEnabledTaskIsDisabled_ClearsNextRunTime()
    {
        var live = new ScheduledTask { MacroFilePath = "test.macro", IsEnabled = true };
        _service.Tasks.Add(live);
        var draft = AutomationTaskSnapshots.Copy(live);
        draft.IsEnabled = false;

        await _service.CommitAsync([draft], TestContext.Current.CancellationToken);

        var committed = Assert.Single(_service.Tasks);
        _ = committed.IsEnabled.Should().BeFalse();
        _ = committed.NextRunTime.Should().BeNull();
    }

    [Fact]
    public async Task CommitAsync_UpdatesTheExistingTaskProperties()
    {
        var original = new ScheduledTask { Name = "Old Name", MacroFilePath = "old.macro" };
        _service.Tasks.Add(original);
        var updated = new ScheduledTask { Id = original.Id, Name = "New Name", MacroFilePath = "new.macro" };

        await _service.CommitAsync([updated], TestContext.Current.CancellationToken);

        _ = Assert.Single(_service.Tasks).Should().BeSameAs(original);
        _ = original.Name.Should().Be("New Name");
        _ = original.MacroFilePath.Should().Be("new.macro");
    }

    [Fact]
    public async Task LoadAsync_SpecificTimeEnabledFuture_RecalculatesNextRunTimeFromScheduledDateTime()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
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

        await _service.LoadAsync();

        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        _ = loaded.IsEnabled.Should().BeTrue();
        _ = loaded.NextRunTime.Should().Be(scheduledUtc);
    }

    [Fact]
    public async Task LoadAsync_SpecificTimePast_DisablesTaskAndClearsNextRunTime()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
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

        await _service.LoadAsync();

        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        _ = loaded.IsEnabled.Should().BeFalse();
        _ = loaded.NextRunTime.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_HugeIntervalTask_DoesNotThrow_AndClampsNextRunTime()
    {
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

        Func<Task> act = async () => await _service.LoadAsync();

        _ = await act.Should().NotThrowAsync();
        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        _ = loaded.IsEnabled.Should().BeTrue();
        _ = loaded.NextRunTime.Should().NotBeNull();
        _ = loaded.NextRunTime.Value.Ticks.Should().Be(DateTime.MaxValue.Ticks);
    }

    [Fact]
    public async Task LoadAsync_SpecificTimeWithoutScheduledDate_DisablesTaskAndClearsNextRunTime()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
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

        await _service.LoadAsync();

        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        _ = loaded.IsEnabled.Should().BeFalse();
        _ = loaded.NextRunTime.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WeeklyEnabled_RecalculatesNextRunTime()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
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
        _ = loaded.NextRunTime.Value.Should().BeOnOrAfter(now);
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
            NextRunTime = _clock.GetUtcNow().UtcDateTime.AddHours(1),
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

        await _service.LoadAsync();

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
            using var service = new SchedulerService(_repository, _executor, _clock);

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
            using var service = new SchedulerService(_repository, _executor, _clock);
            SynchronizationContext.SetSynchronizationContext(previousContext);

            var workerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var workerMayLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var loadTask = Task.Run(async () =>
            {
                workerStarted.SetResult();
                await workerMayLoad.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
                await service.LoadAsync();
            }, TestContext.Current.CancellationToken);

            await workerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
            workerMayLoad.SetResult();
            await synchronizationContext.PostObserved.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);

            _ = loadTask.IsCompleted.Should().BeFalse();
            _ = synchronizationContext.PendingCallbacks.Should().Be(1);
            _ = service.Tasks.Should().BeEmpty();

            synchronizationContext.RunAll();
            await loadTask.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);

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
        var executionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowExecutionToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _executor.ExecuteAsync(Arg.Any<ScheduledTask>(), Arg.Any<CancellationToken>())
            .Returns(unusedCallInfo =>
            {
                _ = executionStarted.TrySetResult();
                return allowExecutionToFinish.Task;
            });

        _ = CreateRunNowTask(_clock);
        _service.Start();
        _clock.Advance(TimeSpan.FromSeconds(1));

        await executionStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, TestContext.Current.CancellationToken);

        var stopTask = Task.Run(_service.StopScheduler, TestContext.Current.CancellationToken);
        await stopTask.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);

        _ = _service.IsRunning.Should().BeFalse();
        _ = allowExecutionToFinish.TrySetResult();
        await _service.Completion.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
    }

    private ScheduledTask CreateRunNowTask(TimeProvider clock)
    {
        var task = new ScheduledTask
        {
            Name = "Run now",
            MacroFilePath = "test.macro",
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Seconds,
            IntervalValue = 30,
            IsEnabled = false,
        };
        _service.Tasks.Add(task);
        task.IsEnabled = true;
        task.NextRunTime = clock.GetUtcNow().UtcDateTime;
        return task;
    }

    public void Dispose() => _service.Dispose();
}
