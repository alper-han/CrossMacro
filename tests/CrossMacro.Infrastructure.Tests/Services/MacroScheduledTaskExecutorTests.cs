
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class MacroScheduledTaskExecutorTests
{
    private readonly IMacroFileManager _fileManager;
    private readonly IMacroPlayer _player;
    private readonly TimeProvider _timeProvider;
    private readonly MacroScheduledTaskExecutor _executor;

    public MacroScheduledTaskExecutorTests()
    {
        _fileManager = Substitute.For<IMacroFileManager>();
        _player = Substitute.For<IMacroPlayer>();
        _timeProvider = Substitute.For<TimeProvider>();

        // Mock time
        _ = _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

        _executor = new MacroScheduledTaskExecutor(
            _fileManager,
            () => _player,
            _timeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFileDoesNotExist_UpdatesStatusAndFails()
    {
        // Arrange
        var task = new ScheduledTask { MacroFilePath = "nonexistent.macro" };
        // We can't easily mock File.Exists since it's static, but the executor checks string.IsNullOrEmpty first.
        // Wait, the executor calls File.Exists directly. This makes it hard to unit test without IFileSystem.
        // However, we can test the behavior when the file is missing by providing a path that definitely doesn't exist 
        // OR by relying on the fact that we are in a unit test environment where that file likely doesn't exist.
        // A better approach for the future would be IFileSystem, but for now we assume it doesn't exist.

        // Act
        await _executor.ExecuteAsync(task);

        // Assert
        _ = task.LastStatus.Should().Be("Macro file not found");
        _ = task.LastRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLoadFails_UpdatesStatusAndFails()
    {
        // Arrange
        // We need to bake a real file or use a path that exists? 
        // The current implementation of MacroScheduledTaskExecutor checks File.Exists(task.MacroFilePath).
        // If we can't mock File.Exists, we can't fully test the success path or load failure path *unless* we create a temp file.

        var tempFile = Path.GetTempFileName();
        try
        {
            var task = new ScheduledTask
            {
                MacroFilePath = tempFile,
                Type = ScheduleType.SpecificTime,
                IsEnabled = true,
            };
            MacroSequence? missingMacro = null;
            _ = _fileManager.LoadAsync(tempFile).Returns(missingMacro);

            // Act
            await _executor.ExecuteAsync(task);

            // Assert
            _ = task.LastStatus.Should().Be("Failed to load macro");
            _ = task.NextRunTime.Should().BeNull();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccess_UpdatesStatusAndNextRunTime()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var task = new ScheduledTask
            {
                MacroFilePath = tempFile,
                Type = ScheduleType.Interval,
                IntervalValue = 10,
                IntervalUnit = IntervalUnit.Seconds,
                IsEnabled = true,
            };

            var macro = new MacroSequence { Name = "Test MacroSequence" };
            _ = _fileManager.LoadAsync(tempFile).Returns(macro);

            // Act
            await _executor.ExecuteAsync(task);

            // Assert
            await _player.Received(1).PlayAsync(macro, Arg.Any<PlaybackOptions>());
            _ = task.LastStatus.Should().Be("Success");
            _ = task.LastRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);

            // Should verify next run time calculation
            // The NextRunTime logic depends on current time + interval. 
            // Since we mocked UtcNow, it should be predictable.
            _ = task.NextRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddSeconds(10));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneTimeTaskSuccess_DisablesTask()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var task = new ScheduledTask
            {
                MacroFilePath = tempFile,
                Type = ScheduleType.SpecificTime,
                IsEnabled = true,
            };

            var macro = new MacroSequence();
            _ = _fileManager.LoadAsync(tempFile).Returns(macro);

            // Act
            await _executor.ExecuteAsync(task);

            // Assert
            _ = task.IsEnabled.Should().BeFalse();
            _ = task.NextRunTime.Should().BeNull();
            _ = task.LastStatus.Should().Be("Success");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlaybackThrows_UpdatesStatusAndFailsGracefully()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var task = new ScheduledTask
            {
                MacroFilePath = tempFile,
                Type = ScheduleType.SpecificTime,
                IsEnabled = true,
            };

            var macro = new MacroSequence();
            _ = _fileManager.LoadAsync(tempFile).Returns(macro);

            _player.When(p => p.PlayAsync(macro, Arg.Any<PlaybackOptions>()))
                   .Do(x => throw new InvalidOperationException("Unexpected crash"));

            // Act
            await _executor.ExecuteAsync(task);

            // Assert
            _ = task.LastStatus.Should().Contain("Error");
            _ = task.LastStatus.Should().Contain("Unexpected crash");
            _ = task.NextRunTime.Should().BeNull();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenTaskSpeedIsInvalid_UsesNormalizedPlaybackSpeed()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var task = new ScheduledTask
            {
                MacroFilePath = tempFile,
                PlaybackSpeed = 0.0,
                IsEnabled = true,
            };

            var macro = new MacroSequence { Events = { new MacroEvent { Type = EventType.MouseMove, X = 0, Y = 0 } } };
            _ = _fileManager.LoadAsync(tempFile).Returns(macro);

            // Act
            await _executor.ExecuteAsync(task);

            // Assert
            await _player.Received(1).PlayAsync(
                macro,
                Arg.Is<PlaybackOptions>(o => o.SpeedMultiplier == PlaybackOptions.MinSpeedMultiplier));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenIntervalLoadFails_ReschedulesNextRun()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var task = new ScheduledTask
            {
                MacroFilePath = tempFile,
                Type = ScheduleType.Interval,
                IntervalValue = 15,
                IntervalUnit = IntervalUnit.Seconds,
                IsEnabled = true,
            };

            MacroSequence? missingIntervalMacro = null;
            _ = _fileManager.LoadAsync(tempFile).Returns(missingIntervalMacro);

            await _executor.ExecuteAsync(task);

            _ = task.LastStatus.Should().Be("Failed to load macro");
            _ = task.NextRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddSeconds(15));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenIntervalPlaybackThrows_ReschedulesNextRun()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var task = new ScheduledTask
            {
                MacroFilePath = tempFile,
                Type = ScheduleType.Interval,
                IntervalValue = 30,
                IntervalUnit = IntervalUnit.Seconds,
                IsEnabled = true,
            };

            var macro = new MacroSequence();
            _ = _fileManager.LoadAsync(tempFile).Returns(macro);
            _player
                .When(p => p.PlayAsync(macro, Arg.Any<PlaybackOptions>()))
                .Do(_ => throw new InvalidOperationException("Unexpected crash"));

            await _executor.ExecuteAsync(task);

            _ = task.LastStatus.Should().Contain("Unexpected crash");
            _ = task.NextRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddSeconds(30));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenWeeklyTaskSuccess_ReschedulesNextRunTime()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var nextLocalTime = _timeProvider.GetUtcNow().ToLocalTime().TimeOfDay.Add(TimeSpan.FromMinutes(30));
            var task = new ScheduledTask
            {
                MacroFilePath = tempFile,
                Type = ScheduleType.Weekly,
                WeeklyDays = ScheduleDays.EveryDay,
                WeeklyTime = nextLocalTime,
                IsEnabled = true,
            };

            var macro = new MacroSequence();
            _ = _fileManager.LoadAsync(tempFile).Returns(macro);

            await _executor.ExecuteAsync(task);

            await _player.Received(1).PlayAsync(macro, Arg.Any<PlaybackOptions>());
            _ = task.LastStatus.Should().Be("Success");
            _ = task.NextRunTime.Should().NotBeNull();
            _ = task.NextRunTime!.Value.Should().BeAfter(_timeProvider.GetUtcNow().UtcDateTime);
            _ = task.IsEnabled.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_UpdatesStatusAndReschedulesIntervalTask()
    {
        var tempFile = Path.GetTempFileName();
        using var cts = new CancellationTokenSource();

        try
        {
            var task = new ScheduledTask
            {
                MacroFilePath = tempFile,
                Type = ScheduleType.Interval,
                IntervalValue = 20,
                IntervalUnit = IntervalUnit.Seconds,
                IsEnabled = true,
            };

            var macro = new MacroSequence();
            var playbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = _fileManager.LoadAsync(tempFile).Returns(macro);
            _ = _player.PlayAsync(macro, Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    _ = playbackStarted.TrySetResult(true);
                    return Task.Delay(Timeout.Infinite, ci.ArgAt<CancellationToken>(2));
                });

            var executionTask = _executor.ExecuteAsync(task, cts.Token);
            _ = await playbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cts.Cancel();
            await executionTask;

            _ = task.LastStatus.Should().Be("Cancelled");
            _ = task.NextRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddSeconds(20));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
