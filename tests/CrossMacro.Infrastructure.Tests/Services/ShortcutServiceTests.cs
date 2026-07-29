
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ShortcutServiceTests : IDisposable
{
    private readonly IMacroFileManager _fileManager;
    private readonly Func<IMacroPlayer> _playerFactory;
    private readonly IMacroPlayer _player;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ShortcutService _service;
    private readonly string _testRootDirectory;
    private readonly string _shortcutsFilePath;

    public ShortcutServiceTests()
    {
        _testRootDirectory = Path.Combine(
            Path.GetTempPath(),
            "crossmacro-tests",
            nameof(ShortcutServiceTests),
            Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_testRootDirectory);
        _shortcutsFilePath = Path.Combine(_testRootDirectory, "shortcuts.json");

        _fileManager = Substitute.For<IMacroFileManager>();
        _player = Substitute.For<IMacroPlayer>();
        _playerFactory = () => _player;
        _hotkeyService = Substitute.For<IGlobalHotkeyService>();

        _service = new ShortcutService(_fileManager, _playerFactory, _hotkeyService, shortcutsFilePath: _shortcutsFilePath);
    }

    public void Dispose()
    {
        _service.Dispose();

        try
        {
            if (Directory.Exists(_testRootDirectory))
            {
                Directory.Delete(_testRootDirectory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort test cleanup tolerates expected filesystem failures.
        }
    }

    [Fact]
    public void Start_SubscribesToHotkeyService()
    {
        _service.Start();

        // We can verify this by checking if IsListening is true, 
        // verifying event subscription is hard with NSubstitute unless we inspect calls to add_Event.
        // But implementation sets IsListening.
        _ = _service.IsListening.Should().BeTrue();
    }

    [Fact]
    public void Stop_UnsubscribesAndSetsListeningFalse()
    {
        _service.Start();
        _service.StopShortcuts();

        _ = _service.IsListening.Should().BeFalse();
    }

    [Fact]
    public void AddTask_AddsToCollection()
    {
        var task = new ShortcutTask();
        _service.AddTask(task);
        _ = _service.Tasks.Should().Contain(task);
    }

    [Fact]
    public void RemoveTask_RemovesFromCollection()
    {
        var task = new ShortcutTask();
        _service.AddTask(task);
        _service.RemoveTask(task.Id);
        _ = _service.Tasks.Should().NotContain(task);
    }

    [Fact]
    public async Task OnRawInputReceived_ExecutesMatchingTask()
    {
        // Arrange
        var task = new ShortcutTask
        {
            Name = "Test",
            MacroFilePath = "test.macro",
            HotkeyString = "F5",
            PlaybackSpeed = 0.0,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _fileManager.LoadAsync(Arg.Any<string>())
            .Returns(Task.FromResult<MacroSequence?>(new MacroSequence { Events = { new MacroEvent() } }));
        _ = _player
            .PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>())
            .Returns(Task.CompletedTask);

        var executed = new TaskCompletionSource<ShortcutExecutedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.ShortcutExecuted += (_, e) =>
        {
            if (e.Task.Id == task.Id)
            {
                executed.TrySetResult(e);
            }
        };

        _service.Start();

        var tempFile = Path.GetTempFileName();
        task.MacroFilePath = tempFile;

        try
        {
            // Act
            _hotkeyService.RawInputReceived += Raise.Event<EventHandler<RawHotkeyInputEventArgs>>(
                this,
                new RawHotkeyInputEventArgs(0, new HashSet<int>(), "F5"));
            var result = await executed.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Assert
            _ = result.Success.Should().BeTrue();
            await _player.Received(1).PlayAsync(
                Arg.Any<MacroSequence>(),
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
    public async Task OnRawInputReceived_DoesNotExecute_IfDisabled()
    {
        // Arrange
        var task = new ShortcutTask
        {
            HotkeyString = "F5",
            MacroFilePath = "test.macro",
            IsEnabled = false,
        };
        _service.AddTask(task);

        _service.Start();

        // Act
        _hotkeyService.RawInputReceived += Raise.Event<EventHandler<RawHotkeyInputEventArgs>>(
            this,
            new RawHotkeyInputEventArgs(0, new HashSet<int>(), "F5"));

        // Assert
        await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>());
    }

    [Fact]
    public async Task OnRawKeyReleased_RunWhileHeldHotkey_DoesNotThrowAndStopsPlayer()
    {
        // Arrange
        var task = new ShortcutTask
        {
            Name = "Held Macro",
            HotkeyString = "Ctrl+F5",
            RunWhileHeld = true,
        };

        var tempFile = Path.GetTempFileName();
        task.MacroFilePath = tempFile;
        task.IsEnabled = true;
        _service.AddTask(task);

        var startedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePlaybackTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = _fileManager.LoadAsync(tempFile).Returns(Task.FromResult<MacroSequence?>(new MacroSequence
        {
            Events = { new MacroEvent { Type = EventType.KeyPress, KeyCode = 30, Timestamp = 0 } },
        }));

        _ = _player
            .PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(unusedCallInfo =>
            {
            _ = startedTcs.TrySetResult(true);
                return releasePlaybackTcs.Task;
            });

        _service.Start();

        try
        {
            _hotkeyService.RawInputReceived += Raise.Event<EventHandler<RawHotkeyInputEventArgs>>(
                this,
                new RawHotkeyInputEventArgs(63, new HashSet<int> { 29 }, "Ctrl+F5"));

            _ = await startedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

            var releaseException = Record.Exception(() =>
                _hotkeyService.RawKeyReleased += Raise.Event<EventHandler<RawHotkeyInputEventArgs>>(
                    this,
                    new RawHotkeyInputEventArgs(63, new HashSet<int> { 29 }, string.Empty)));

            _ = releaseException.Should().BeNull();

            _player.Received(1).StopPlayback();
        }
        finally
        {
            _ = releasePlaybackTcs.TrySetResult(true);
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task RunTaskAsync_WhenCancelledDuringPlayback_UpdatesTaskAsStopped()
    {
        var tempFile = Path.GetTempFileName();
        using var cts = new CancellationTokenSource();

        // The service marshals status updates through the synchronization context captured
        // at construction. xUnit's async context does not serialize posted callbacks with
        // await continuations, so build a dedicated service with no ambient context to make
        // SafeUpdate run inline and the post-await assertion deterministic.
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(syncContext: null);

        try
        {
            using var service = new ShortcutService(_fileManager, _playerFactory, _hotkeyService, shortcutsFilePath: _shortcutsFilePath);
            var task = new ShortcutTask
            {
                Name = "Manual Run",
                HotkeyString = "F5",
                MacroFilePath = tempFile,
                IsEnabled = true,
            };

            service.AddTask(task);
            _ = _fileManager.LoadAsync(tempFile).Returns(Task.FromResult<MacroSequence?>(new MacroSequence
            {
                Events = { new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 } },
            }));

            var playbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    _ = playbackStarted.TrySetResult(true);
                    return Task.Delay(Timeout.Infinite, ci.ArgAt<CancellationToken>(2));
                });

            var runTask = service.RunTaskAsync(task.Id, cts.Token);
            _ = await playbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cts.Cancel();
            await runTask;

            _ = task.LastStatus.Should().Be("Stopped");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_AndLoadAsync_RoundTripPreservesPersistedContractAndComputedState()
    {
        var persistedTask = new ShortcutTask
        {
            Name = "Launch Macro",
            MacroFilePath = "/tmp/sample.macro",
            HotkeyString = "Ctrl+Shift+M",
            PlaybackSpeed = 2.5,
            IsEnabled = true,
            LoopEnabled = true,
            RepeatCount = 4,
            RepeatDelayMs = 125,
            LastStatus = "Success",
            LastTriggeredTime = new DateTime(2026, 4, 27, 10, 30, 0, DateTimeKind.Utc),
        };

        _service.AddTask(persistedTask);

        await _service.SaveAsync();

        var savedJson = await File.ReadAllTextAsync(_shortcutsFilePath, CancellationToken.None);
        _ = savedJson.Should().Contain("\"macroFilePath\"");
        _ = savedJson.Should().Contain("\"hotkeyString\"");
        _ = savedJson.Should().Contain("\"loopEnabled\": true");
        _ = savedJson.Should().NotContain("\"isLoopEnabled\"");

        var reloadedService = new ShortcutService(_fileManager, _playerFactory, _hotkeyService, shortcutsFilePath: _shortcutsFilePath);

        try
        {
            await reloadedService.LoadAsync();

            _ = reloadedService.Tasks.Should().ContainSingle();
            var loadedTask = reloadedService.Tasks[0];
            _ = loadedTask.Name.Should().Be("Launch Macro");
            _ = loadedTask.MacroFilePath.Should().Be("/tmp/sample.macro");
            _ = loadedTask.HotkeyString.Should().Be("Ctrl+Shift+M");
            _ = loadedTask.PlaybackSpeed.Should().Be(2.5);
            _ = loadedTask.IsEnabled.Should().BeTrue();
            _ = loadedTask.LoopEnabled.Should().BeTrue();
            _ = loadedTask.RunWhileHeld.Should().BeFalse();
            _ = loadedTask.IsLoopEnabled.Should().BeTrue();
            _ = loadedTask.CanBeEnabled.Should().BeTrue();
            _ = loadedTask.RepeatCount.Should().Be(4);
            _ = loadedTask.RepeatDelayMs.Should().Be(125);
            _ = loadedTask.LastStatus.Should().Be("Success");
            _ = loadedTask.LastTriggeredTime.Should().Be(new DateTime(2026, 4, 27, 10, 30, 0, DateTimeKind.Utc));
        }
        finally
        {
            reloadedService.Dispose();
        }
    }

    [Fact]
    public async Task LoadAsync_UsesExplicitShortcutTaskContextContract()
    {
        var expectedTasks = new List<ShortcutTask>
        {
            new()
            {
                Name = "Typed Context Task",
                MacroFilePath = "typed.macro",
                HotkeyString = "F6",
                RunWhileHeld = true,
                RepeatDelayMs = 42,
            },
        };

        await File.WriteAllTextAsync(
            _shortcutsFilePath,
            JsonSerializer.Serialize(expectedTasks, CrossMacroJsonContext.Default.ListShortcutTask),
            CancellationToken.None);

        await _service.LoadAsync();

        _ = _service.Tasks.Should().ContainSingle();
        var loadedTask = _service.Tasks[0];
        _ = loadedTask.Name.Should().Be("Typed Context Task");
        _ = loadedTask.RunWhileHeld.Should().BeTrue();
        _ = loadedTask.LoopEnabled.Should().BeFalse();
        _ = loadedTask.IsLoopEnabled.Should().BeTrue();
        _ = loadedTask.RepeatDelayMs.Should().Be(42);
    }
}
