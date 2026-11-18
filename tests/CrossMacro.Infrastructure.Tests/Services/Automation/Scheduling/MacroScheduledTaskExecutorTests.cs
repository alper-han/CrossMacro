namespace CrossMacro.Infrastructure.Tests.Services.Automation.Scheduling;

public sealed class MacroScheduledTaskExecutorTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("crossmacro-scheduled-executor-");
    private readonly IMacroFileManager _fileManager = Substitute.For<IMacroFileManager>();
    private readonly IMacroPlayer _player = Substitute.For<IMacroPlayer>();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly MacroScheduledTaskExecutor _executor;

    public MacroScheduledTaskExecutorTests()
    {
        _executor = new MacroScheduledTaskExecutor(_fileManager, () => _player, _timeProvider);
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    [Fact]
    public void Constructor_WhenFileManagerIsNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new MacroScheduledTaskExecutor(null!, () => _player, _timeProvider));
    }

    [Fact]
    public void Constructor_WhenPlayerFactoryIsNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new MacroScheduledTaskExecutor(_fileManager, null!, _timeProvider));
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new MacroScheduledTaskExecutor(_fileManager, () => _player, null!));
    }

    [Fact]
    public async Task ExecuteAsync_WhenFileDoesNotExist_UpdatesStatusAndFails()
    {
        var task = new ScheduledTask { MacroFilePath = Path.Combine(_directory.FullName, "missing.macro") };

        await _executor.ExecuteAsync(task, TestContext.Current.CancellationToken);

        _ = task.LastStatus.Should().Be("Macro file not found");
        _ = task.LastRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        await _fileManager.DidNotReceive().LoadAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenLoadFails_UpdatesStatusAndFails()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = CreateMacroFile(),
            Type = ScheduleType.SpecificTime,
            IsEnabled = true,
        };
        MacroSequence? missingMacro = null;
        _ = _fileManager.LoadAsync(task.MacroFilePath).Returns(missingMacro);

        await _executor.ExecuteAsync(task, TestContext.Current.CancellationToken);

        _ = task.LastStatus.Should().Be("Failed to load macro");
        _ = task.NextRunTime.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccess_UpdatesStatusAndNextRunTime()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = CreateMacroFile(),
            Type = ScheduleType.Interval,
            IntervalValue = 10,
            IntervalUnit = IntervalUnit.Seconds,
            IsEnabled = true,
        };
        var macro = new MacroSequence { Name = "Test MacroSequence" };
        _ = _fileManager.LoadAsync(task.MacroFilePath).Returns(macro);

        await _executor.ExecuteAsync(task, TestContext.Current.CancellationToken);

        await _player.Received(1).PlayAsync(macro, Arg.Any<PlaybackOptions>(), TestContext.Current.CancellationToken);
        _ = task.LastStatus.Should().Be("Success");
        _ = task.LastRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        _ = task.NextRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddSeconds(10));
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneTimeTaskSuccess_DisablesTask()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = CreateMacroFile(),
            Type = ScheduleType.SpecificTime,
            IsEnabled = true,
        };
        _ = _fileManager.LoadAsync(task.MacroFilePath).Returns(new MacroSequence());

        await _executor.ExecuteAsync(task, TestContext.Current.CancellationToken);

        _ = task.IsEnabled.Should().BeFalse();
        _ = task.NextRunTime.Should().BeNull();
        _ = task.LastStatus.Should().Be("Success");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlaybackThrows_UpdatesStatusAndFailsGracefully()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = CreateMacroFile(),
            Type = ScheduleType.SpecificTime,
            IsEnabled = true,
        };
        var macro = new MacroSequence();
        _ = _fileManager.LoadAsync(task.MacroFilePath).Returns(macro);
        _player.When(p => p.PlayAsync(macro, Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Unexpected crash"));

        await _executor.ExecuteAsync(task, TestContext.Current.CancellationToken);

        _ = task.LastStatus.Should().Contain("Error");
        _ = task.LastStatus.Should().Contain("Unexpected crash");
        _ = task.NextRunTime.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlayerIsBusy_SkipsAndReschedulesIntervalTask()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = CreateMacroFile(),
            Type = ScheduleType.Interval,
            IntervalValue = 5,
            IntervalUnit = IntervalUnit.Minutes,
            IsEnabled = true,
        };
        var macro = new MacroSequence();
        _ = _fileManager.LoadAsync(task.MacroFilePath).Returns(macro);
        _player.When(p => p.PlayAsync(macro, Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new PlaybackAlreadyInProgressException());

        await _executor.ExecuteAsync(task, TestContext.Current.CancellationToken);

        _ = task.LastStatus.Should().Be("Skipped (playback busy)");
        _ = task.NextRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddMinutes(5));
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnrelatedErrorMentionsProgress_ReportsError()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = CreateMacroFile(),
            Type = ScheduleType.SpecificTime,
            IsEnabled = true,
        };
        var macro = new MacroSequence();
        _ = _fileManager.LoadAsync(task.MacroFilePath).Returns(macro);
        _player.When(p => p.PlayAsync(macro, Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("A hotkey capture is already in progress."));

        await _executor.ExecuteAsync(task, TestContext.Current.CancellationToken);

        _ = task.LastStatus.Should().StartWith("Error");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTaskSpeedIsInvalid_UsesNormalizedPlaybackSpeed()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = CreateMacroFile(),
            PlaybackSpeed = 0.0,
            IsEnabled = true,
        };
        var macro = new MacroSequence { Events = { new MacroEvent { Type = EventType.MouseMove, X = 0, Y = 0 } } };
        _ = _fileManager.LoadAsync(task.MacroFilePath).Returns(macro);

        await _executor.ExecuteAsync(task, TestContext.Current.CancellationToken);

        await _player.Received(1).PlayAsync(
            macro,
            Arg.Is<PlaybackOptions>(o => double.Equals(o.SpeedMultiplier, PlaybackOptions.MinSpeedMultiplier)),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIntervalLoadFails_ReschedulesNextRun()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = CreateMacroFile(),
            Type = ScheduleType.Interval,
            IntervalValue = 15,
            IntervalUnit = IntervalUnit.Seconds,
            IsEnabled = true,
        };
        MacroSequence? missingMacro = null;
        _ = _fileManager.LoadAsync(task.MacroFilePath).Returns(missingMacro);

        await _executor.ExecuteAsync(task, TestContext.Current.CancellationToken);

        _ = task.LastStatus.Should().Be("Failed to load macro");
        _ = task.NextRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddSeconds(15));
    }

    [Fact]
    public async Task ExecuteAsync_WhenIntervalPlaybackThrows_ReschedulesNextRun()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = CreateMacroFile(),
            Type = ScheduleType.Interval,
            IntervalValue = 30,
            IntervalUnit = IntervalUnit.Seconds,
            IsEnabled = true,
        };
        var macro = new MacroSequence();
        _ = _fileManager.LoadAsync(task.MacroFilePath).Returns(macro);
        _player.When(p => p.PlayAsync(macro, Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Unexpected crash"));

        await _executor.ExecuteAsync(task, TestContext.Current.CancellationToken);

        _ = task.LastStatus.Should().Contain("Unexpected crash");
        _ = task.NextRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddSeconds(30));
    }

    [Fact]
    public async Task ExecuteAsync_WhenWeeklyTaskSuccess_ReschedulesNextRunTime()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = CreateMacroFile(),
            Type = ScheduleType.Weekly,
            WeeklyDays = ScheduleDays.EveryDay,
            WeeklyTime = _timeProvider.GetLocalNow().TimeOfDay.Add(TimeSpan.FromMinutes(30)),
            IsEnabled = true,
        };
        var macro = new MacroSequence();
        _ = _fileManager.LoadAsync(task.MacroFilePath).Returns(macro);

        await _executor.ExecuteAsync(task, TestContext.Current.CancellationToken);

        await _player.Received(1).PlayAsync(macro, Arg.Any<PlaybackOptions>(), TestContext.Current.CancellationToken);
        _ = task.LastStatus.Should().Be("Success");
        _ = task.NextRunTime.Should().BeAfter(_timeProvider.GetUtcNow().UtcDateTime);
        _ = task.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_UpdatesStatusAndReschedulesIntervalTask()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var task = new ScheduledTask
        {
            MacroFilePath = CreateMacroFile(),
            Type = ScheduleType.Interval,
            IntervalValue = 20,
            IntervalUnit = IntervalUnit.Seconds,
            IsEnabled = true,
        };
        var macro = new MacroSequence();
        var playbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _fileManager.LoadAsync(task.MacroFilePath).Returns(macro);
        _ = _player.PlayAsync(macro, Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _ = playbackStarted.TrySetResult(true);
                return Task.Delay(Timeout.Infinite, ci.ArgAt<CancellationToken>(2));
            });

        var executionTask = _executor.ExecuteAsync(task, cts.Token);
        _ = await playbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken);
        await cts.CancelAsync();
        await executionTask;

        _ = task.LastStatus.Should().Be("Cancelled");
        _ = task.NextRunTime.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddSeconds(20));
    }

    private string CreateMacroFile()
    {
        var path = Path.Combine(_directory.FullName, $"{Guid.NewGuid():N}.macro");
        File.WriteAllText(path, string.Empty);
        return path;
    }
}
