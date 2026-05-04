using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class SchedulerServiceTests
{
    private readonly IScheduledTaskRepository _repository;
    private readonly IScheduledTaskExecutor _executor;
    private readonly ITimeProvider _timeProvider;
    private readonly SchedulerService _service;

    public SchedulerServiceTests()
    {
        _repository = Substitute.For<IScheduledTaskRepository>();
        _executor = Substitute.For<IScheduledTaskExecutor>();
        _timeProvider = Substitute.For<ITimeProvider>();
        
        // Default time
        _timeProvider.UtcNow.Returns(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        _service = new SchedulerService(_repository, _executor, _timeProvider);
    }

    [Fact]
    public void Start_SetsIsRunningToTrue()
    {
        _service.Start();
        _service.IsRunning.Should().BeTrue();
        _service.Stop();
    }

    [Fact]
    public void Stop_SetsIsRunningToFalse()
    {
        _service.Start();
        _service.Stop();
        _service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void AddTask_AddsToCollection()
    {
        var task = new ScheduledTask();
        _service.AddTask(task);
        _service.Tasks.Should().Contain(task);
    }

    [Fact]
    public void RemoveTask_RemovesFromCollection()
    {
        var task = new ScheduledTask();
        _service.AddTask(task);
        _service.RemoveTask(task.Id);
        _service.Tasks.Should().NotContain(task);
    }

    [Fact]
    public void SetTaskEnabled_WhenTrue_CalculatesNextRunTime()
    {
        // Arrange
        var task = new ScheduledTask { MacroFilePath = "test.macro", Type = ScheduleType.Interval, IntervalValue = 60, IntervalUnit = IntervalUnit.Seconds };
        _service.AddTask(task);

        // Act
        _service.SetTaskEnabled(task.Id, true);

        // Assert
        var t = _service.Tasks.First(x => x.Id == task.Id);
        t.IsEnabled.Should().BeTrue();
        t.NextRunTime.Should().Be(_timeProvider.UtcNow.AddSeconds(60));
    }

    [Fact]
    public void SetTaskEnabled_WhenFalse_ClearsNextRunTime()
    {
        // Arrange
        var task = new ScheduledTask { MacroFilePath = "test.macro", IsEnabled = true };
        _service.AddTask(task); // Adds and calculates
        
        // Act
        _service.SetTaskEnabled(task.Id, false);

        // Assert
        var t = _service.Tasks.First(x => x.Id == task.Id);
        t.IsEnabled.Should().BeFalse();
        t.NextRunTime.Should().BeNull();
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
            MacroFilePath = "new.macro" 
        };

        // Act
        _service.UpdateTask(updated);

        // Assert
        var t = _service.Tasks.First(x => x.Id == original.Id);
        t.Name.Should().Be("New Name");
        t.MacroFilePath.Should().Be("new.macro");
    }

    [Fact]
    public async Task LoadAsync_SpecificTimeEnabledFuture_RecalculatesNextRunTimeFromScheduledDateTime()
    {
        // Arrange
        var now = _timeProvider.UtcNow;
        var scheduledUtc = now.AddHours(3);
        var task = new ScheduledTask
        {
            Name = "Future task",
            MacroFilePath = "task.macro",
            Type = ScheduleType.SpecificTime,
            ScheduledDateTime = scheduledUtc
        };
        task.IsEnabled = true;
        task.NextRunTime = now.AddMinutes(5); // stale persisted value

        _repository.LoadAsync().Returns(Task.FromResult(new List<ScheduledTask> { task }));

        // Act
        await _service.LoadAsync();

        // Assert
        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        loaded.IsEnabled.Should().BeTrue();
        loaded.NextRunTime.Should().Be(scheduledUtc);
    }

    [Fact]
    public async Task LoadAsync_SpecificTimePast_DisablesTaskAndClearsNextRunTime()
    {
        // Arrange
        var now = _timeProvider.UtcNow;
        var task = new ScheduledTask
        {
            Name = "Past task",
            MacroFilePath = "task.macro",
            Type = ScheduleType.SpecificTime,
            ScheduledDateTime = now.AddMinutes(-10)
        };
        task.IsEnabled = true;
        task.NextRunTime = now.AddHours(10); // stale persisted value

        _repository.LoadAsync().Returns(Task.FromResult(new List<ScheduledTask> { task }));

        // Act
        await _service.LoadAsync();

        // Assert
        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        loaded.IsEnabled.Should().BeFalse();
        loaded.NextRunTime.Should().BeNull();
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
            IntervalValue = int.MaxValue
        };
        task.IsEnabled = true;

        _repository.LoadAsync().Returns(Task.FromResult(new List<ScheduledTask> { task }));

        // Act
        Func<Task> act = async () => await _service.LoadAsync();

        // Assert
        await act.Should().NotThrowAsync();
        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        loaded.IsEnabled.Should().BeTrue();
        loaded.NextRunTime.Should().NotBeNull();
        loaded.NextRunTime!.Value.Ticks.Should().Be(DateTime.MaxValue.Ticks);
    }

    [Fact]
    public async Task LoadAsync_SpecificTimeWithoutScheduledDate_DisablesTaskAndClearsNextRunTime()
    {
        // Arrange
        var now = _timeProvider.UtcNow;
        var task = new ScheduledTask
        {
            Name = "Invalid specific-time",
            MacroFilePath = "task.macro",
            Type = ScheduleType.SpecificTime,
            ScheduledDateTime = null
        };
        task.IsEnabled = true;
        task.NextRunTime = now.AddHours(5); // stale persisted value

        _repository.LoadAsync().Returns(Task.FromResult(new List<ScheduledTask> { task }));

        // Act
        await _service.LoadAsync();

        // Assert
        var loaded = _service.Tasks.Should().ContainSingle().Subject;
        loaded.IsEnabled.Should().BeFalse();
        loaded.NextRunTime.Should().BeNull();
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
            IntervalValue = 30
        };
        validTask.IsEnabled = true;

        _repository.LoadAsync().Returns(Task.FromResult(new List<ScheduledTask> { null!, validTask }));

        // Act
        await _service.LoadAsync();

        // Assert
        _service.Tasks.Should().ContainSingle();
        _service.Tasks[0].Id.Should().Be(validTask.Id);
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
                IsEnabled = true
            };

            _repository.LoadAsync().Returns(Task.FromResult(new List<ScheduledTask> { task }));
            var service = new SchedulerService(_repository, _executor, _timeProvider);

            var loadTask = service.LoadAsync();
            SynchronizationContext.SetSynchronizationContext(previousContext);

            await loadTask;

            loadTask.IsCompletedSuccessfully.Should().BeTrue();
            service.Tasks.Should().ContainSingle();
            synchronizationContext.PendingCallbacks.Should().Be(0);
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
                IsEnabled = true
            };

            _repository.LoadAsync().Returns(Task.FromResult(new List<ScheduledTask> { task }));
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

            await workerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            workerMayLoad.SetResult();
            await synchronizationContext.PostObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

            loadTask.IsCompleted.Should().BeFalse();
            synchronizationContext.PendingCallbacks.Should().Be(1);
            service.Tasks.Should().BeEmpty();

            synchronizationContext.RunAll();
            await loadTask.WaitAsync(TimeSpan.FromSeconds(2));

            service.Tasks.Should().ContainSingle();
            synchronizationContext.PendingCallbacks.Should().Be(0);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public async Task Stop_WhenTimerLoopIsBusyWithExecution_ReturnsWithoutWaitingForHungExecution()
    {
        var now = _timeProvider.UtcNow;
        var task = new ScheduledTask
        {
            Name = "Run now",
            MacroFilePath = "task.macro",
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Seconds,
            IntervalValue = 30,
            IsEnabled = false
        };

        var executionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _executor.ExecuteAsync(Arg.Any<ScheduledTask>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                executionStarted.TrySetResult();
                return Task.Delay(Timeout.Infinite, CancellationToken.None);
            });

        _service.AddTask(task);
        task.IsEnabled = true;
        task.NextRunTime = now;
        _service.Start();

        await executionStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var stopTask = Task.Run(_service.Stop);
        await stopTask.WaitAsync(TimeSpan.FromSeconds(2));

        _service.IsRunning.Should().BeFalse();
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

            PostObserved.TrySetResult();
        }

        public void RunAll()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) workItem;

                lock (_pendingCallbacks)
                {
                    if (_pendingCallbacks.Count == 0)
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
