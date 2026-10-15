using System.Threading.Channels;

namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ShortcutPressReleaseTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "crossmacro-tests", nameof(ShortcutPressReleaseTests), Guid.NewGuid().ToString("N"));
    private readonly IGlobalHotkeyService _hotkeys = Substitute.For<IGlobalHotkeyService>();
    private readonly IWindowManager _window = Substitute.For<IWindowManager>();
    private readonly IKeyCodeMapper _mapper = Substitute.For<IKeyCodeMapper>();
    private readonly ConcurrentQueue<(int Code, bool Down)> _output = new();
    private readonly Channel<ShortcutExecutedEventArgs> _executions = Channel.CreateUnbounded<ShortcutExecutedEventArgs>();
    private readonly TaskCompletionSource _pressSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _continuePress = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly MacroFileManager _files;
    private readonly ShortcutService _service;
    private bool _pausePress;
    private int _waits;
    private Func<IMacroPlayer>? _playerOverride;

    public ShortcutPressReleaseTests()
    {
        Directory.CreateDirectory(_root);
        _mapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _mapper.GetKeyName(248).Returns("MICMUTE");
        _mapper.GetKeyName(249).Returns("RELEASE");
        _files = new MacroFileManager(() => _mapper);
        _service = new ShortcutService(_files, () => _playerOverride?.Invoke() ?? CreatePlayer(), _hotkeys,
            shortcutsFilePath: Path.Combine(_root, "shortcuts.json"), windowManager: _window,
            hotkeyNormalizer: new ShortcutHotkeyNormalizer(_mapper, new HotkeyStringBuilder(_mapper), new MouseButtonMapper()));
        _service.ShortcutExecuted += (_, e) => _executions.Writer.TryWrite(e);
        _service.Start();
    }

    [Fact]
    public async Task HeldGesture_RunsEachEdgeOnce_AndIgnoresUnrelatedRelease()
    {
        await AddTaskAsync(sameMacro: true);
        var cycle = PressAsync();
        _ = (await NextExecutionAsync()).Success.Should().BeTrue();
        await PressAsync();
        Release(999);
        _ = _output.ToArray().Should().Equal((248, true), (248, false));
        _ = cycle.IsCompleted.Should().BeFalse();

        Release();
        await cycle.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = _output.ToArray().Should().Equal((248, true), (248, false), (248, true), (248, false));
    }

    [Fact]
    public async Task FastRelease_WaitsForPressMacroToFinish_BeforeReleaseMacro()
    {
        _pausePress = true;
        await AddTaskAsync();
        var cycle = PressAsync();
        await _pressSeen.Task.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        Release();
        _ = _output.ToArray().Should().Equal((248, true));

        _continuePress.TrySetResult();
        await cycle.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = _output.ToArray().Should().Equal((248, true), (248, false), (249, true), (249, false));
    }

    [Fact]
    public async Task RapidGestures_QueueCompletePairs_WithoutToggleStopOrDebounceLoss()
    {
        _pausePress = true;
        await AddTaskAsync();
        var first = PressAsync();
        await _pressSeen.Task.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        Release();
        var second = PressAsync();
        Release();
        _continuePress.TrySetResult();

        await Task.WhenAll(first, second).WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = _output.ToArray().Should().Equal(
            (248, true), (248, false), (249, true), (249, false),
            (248, true), (248, false), (249, true), (249, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledQueuedRun_PreservesPredecessorOrder_AndCancelsPromptly(bool successorAlreadyQueued)
    {
        _pausePress = true;
        var task = await AddTaskAsync();
        var press = await _files.LoadAsync(task.MacroFilePath);
        var release = await _files.LoadAsync(task.ReleaseMacroFilePath);
        var cachedFiles = Substitute.For<IMacroFileManager>();
        cachedFiles.LoadAsync(task.MacroFilePath).Returns(Task.FromResult(press));
        cachedFiles.LoadAsync(task.ReleaseMacroFilePath).Returns(Task.FromResult(release));
        using var service = new ShortcutService(cachedFiles, CreatePlayer, _hotkeys,
            shortcutsFilePath: Path.Combine(_root, "queue.json"), windowManager: _window,
            hotkeyNormalizer: new ShortcutHotkeyNormalizer(_mapper, new HotkeyStringBuilder(_mapper), new MouseButtonMapper()));
        service.AddTask(task);
        service.Start();
        Task PressAsync() => service.HandleRawInputAsync(new RawHotkeyInputEventArgs(274, new HashSet<int>(), "Mouse Middle"));

        var first = PressAsync();
        await _pressSeen.Task.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var cancelled = service.RunTaskAsync(task.Id, cancellation.Token);
        Release();
        Task? successor = successorAlreadyQueued ? PressAsync() : null;
        if (successorAlreadyQueued) { Release(); }
        try
        {
            cancellation.Cancel();
            await cancelled.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
            if (successor is null)
            {
                successor = PressAsync();
                Release();
            }
            _ = _output.ToArray().Should().Equal((248, true));
        }
        finally
        {
            _continuePress.TrySetResult();
            await first.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
            if (successor is not null)
            {
                await successor.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
            }
        }
        _ = _output.ToArray().Should().Equal(
            (248, true), (248, false), (249, true), (249, false),
            (248, true), (248, false), (249, true), (249, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedOrFailedPress_IgnoresRepeatedDownUntilPhysicalRelease(bool failedPress)
    {
        var task = await AddTaskAsync();
        task.Hotkeys.Clear();
        task.Hotkeys.Add("F7");
        if (failedPress)
        {
            File.Delete(task.MacroFilePath);
        }
        else
        {
            task.WindowRules.Add(new ShortcutWindowRule { Field = TriggerField.WindowClass, MatchMode = TriggerMatchMode.Equals, Value = "target" });
            _window.IsSupported.Returns(returnThis: true);
            _window.GetActiveWindowAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<WindowInfo?>(new WindowInfo { Class = "other" }));
        }
        await _service.HandleRawInputAsync(new RawHotkeyInputEventArgs(65, new HashSet<int>(), "F7"));
        if (failedPress)
        {
            _ = (await NextExecutionAsync()).Success.Should().BeFalse();
            await SaveMacroAsync(task.MacroFilePath, 248, 0);
        }
        else
        {
            _window.GetActiveWindowAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<WindowInfo?>(new WindowInfo { Class = "target" }));
        }

        var repeatedDown = _service.HandleRawInputAsync(new RawHotkeyInputEventArgs(65, new HashSet<int>(), "F7"));
        Release(65);
        await repeatedDown.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = _output.Should().BeEmpty();

        var nextPress = _service.HandleRawInputAsync(new RawHotkeyInputEventArgs(65, new HashSet<int>(), "F7"));
        Release(65);
        await nextPress.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = _output.ToArray().Should().Equal((248, true), (248, false), (249, true), (249, false));
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("remove")]
    [InlineData("stop")]
    public async Task LifecycleChange_DropsPendingRelease_AndDoesNotRunOldGesture(string change)
    {
        var task = await AddTaskAsync();
        var cycle = PressAsync();
        _ = (await NextExecutionAsync()).Success.Should().BeTrue();
        switch (change)
        {
            case "disable": _service.SetTaskEnabled(task.Id, enabled: false); break;
            case "remove": _service.RemoveTask(task.Id); break;
            case "stop": _service.StopShortcuts(); break;
        }
        Release();
        await cycle.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = _output.ToArray().Should().Equal((248, true), (248, false));
    }

    [Fact]
    public async Task ReleaseDuringWindowLookup_IsRemembered_AndUsesTheAcceptedPressScope()
    {
        var task = await AddTaskAsync();
        task.WindowRules.Add(new ShortcutWindowRule { Field = TriggerField.WindowClass, MatchMode = TriggerMatchMode.Equals, Value = "target" });
        _window.IsSupported.Returns(returnThis: true);
        var lookup = new TaskCompletionSource<WindowInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _window.GetActiveWindowAsync(Arg.Any<CancellationToken>()).Returns(lookup.Task);
        var cycle = PressAsync();
        Release();
        lookup.TrySetResult(new WindowInfo { Class = "target" });

        await cycle.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = _output.ToArray().Should().Equal((248, true), (248, false), (249, true), (249, false));
    }

    [Fact]
    public async Task StopDuringWindowLookup_PreventsBothOldActions()
    {
        var task = await AddTaskAsync();
        task.WindowRules.Add(new ShortcutWindowRule { Field = TriggerField.WindowClass, MatchMode = TriggerMatchMode.Equals, Value = "target" });
        _window.IsSupported.Returns(returnThis: true);
        var lookup = new TaskCompletionSource<WindowInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _window.GetActiveWindowAsync(Arg.Any<CancellationToken>()).Returns(lookup.Task);
        var cycle = PressAsync();
        _service.StopShortcuts();
        lookup.TrySetResult(new WindowInfo { Class = "target" });

        await cycle.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = _output.Should().BeEmpty();
    }

    [Fact]
    public async Task StopDuringPlayerCleanup_RejectsNewGesture_AndDoesNotReplayItAfterRestart()
    {
        _pausePress = true;
        var pair = await AddTaskAsync();
        var normal = new ShortcutTask { MacroFilePath = pair.MacroFilePath, Hotkeys = { "Mouse Left" }, IsEnabled = true };
        _service.AddTask(normal);
        var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var continueCleanup = new ManualResetEventSlim();
        var players = 0;
        _playerOverride = () => Interlocked.Increment(ref players) is 1
            ? new CleanupGatePlayer(CreatePlayer(), cleanupEntered, continueCleanup)
            : CreatePlayer();
        var normalExecution = _service.HandleRawInputAsync(new RawHotkeyInputEventArgs(272, new HashSet<int>(), "Mouse Left"));
        await _pressSeen.Task.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        var stopping = Task.Run(_service.StopShortcuts, TestContext.Current.CancellationToken);
        Task gesture = Task.CompletedTask;
        try
        {
            await cleanupEntered.Task.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
            _pausePress = false;
            gesture = PressAsync();
        }
        finally
        {
            continueCleanup.Set();
            await stopping.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
            await normalExecution.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        }
        _service.Start();
        Release();
        await gesture.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = _output.ToArray().Should().Equal((248, true), (248, false));
    }

    [Fact]
    public async Task SaveAndReload_PreservesExecutableReleaseAction()
    {
        await AddTaskAsync();
        await _service.SaveAsync();
        await _service.LoadAsync();
        var cycle = PressAsync();
        _ = (await NextExecutionAsync()).Success.Should().BeTrue();
        Release();
        await cycle.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = _output.ToArray().Should().Equal((248, true), (248, false), (249, true), (249, false));
    }

    [Fact]
    public async Task ConfigurationCommit_CancelsOldGestureAndUsesEditedReleaseOnNextPress()
    {
        var task = await AddTaskAsync();
        var oldCycle = PressAsync();
        _ = (await NextExecutionAsync()).Success.Should().BeTrue();
        var replacement = Path.Combine(_root, "replacement.macro");
        _mapper.GetKeyName(250).Returns("NEW_RELEASE");
        await SaveMacroAsync(replacement, 250, 0);
        var edited = AutomationTaskSnapshots.Copy(task);
        edited.ReleaseMacroFilePath = replacement;
        await _service.CommitAsync([edited], TestContext.Current.CancellationToken);
        Release();
        await oldCycle.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = _output.ToArray().Should().Equal((248, true), (248, false));

        var nextCycle = PressAsync();
        _ = (await NextExecutionAsync()).Success.Should().BeTrue();
        Release();
        await nextCycle.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = _output.ToArray().Should().Equal((248, true), (248, false), (248, true), (248, false), (250, true), (250, false));
    }

    [Fact]
    public async Task FailedPress_DoesNotExecuteAnUnpairedReleaseAction()
    {
        var task = await AddTaskAsync();
        File.Delete(task.MacroFilePath);
        var cycle = PressAsync();
        Release();
        await cycle.WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = (await NextExecutionAsync()).Success.Should().BeFalse();
        _ = _output.Should().BeEmpty();
    }

    [Fact]
    public async Task ManualRun_ExecutesBothMacros_WithoutWaitingForPhysicalRelease()
    {
        var task = await AddTaskAsync();
        await _service.RunTaskAsync(task.Id, cancellationToken: TestContext.Current.CancellationToken).WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);
        _ = _output.ToArray().Should().Equal((248, true), (248, false), (249, true), (249, false));
    }

    private Task PressAsync() => _service.HandleRawInputAsync(new RawHotkeyInputEventArgs(274, new HashSet<int>(), "Mouse Middle"));
    private void Release(int code = 274) => _hotkeys.RawKeyReleased += Raise.Event<EventHandler<RawHotkeyInputEventArgs>>(
        this, new RawHotkeyInputEventArgs(code, new HashSet<int>(), string.Empty));
    private async Task<ShortcutExecutedEventArgs> NextExecutionAsync() => await _executions.Reader.ReadAsync().AsTask().WaitAsync(Timeout, TimeProvider.System, TestContext.Current.CancellationToken);

    private async Task<ShortcutTask> AddTaskAsync(bool sameMacro = false)
    {
        var press = Path.Combine(_root, "press.macro");
        var release = sameMacro ? press : Path.Combine(_root, "release.macro");
        await SaveMacroAsync(press, 248, _pausePress ? 1000 : 0);
        if (!sameMacro)
        {
            await SaveMacroAsync(release, 249, 0);
        }
        var task = new ShortcutTask
        {
            MacroFilePath = press,
            ReleaseMacroFilePath = release,
            Hotkeys = { "Mouse Middle" },
            RunOnPressAndRelease = true,
            IsEnabled = true,
        };
        _service.AddTask(task);
        return task;
    }

    private Task SaveMacroAsync(string path, int code, int delay) => _files.SaveAsync(new MacroSequence
    {
        Name = "edge",
        IsAbsoluteCoordinates = false,
        Events =
        {
            new() { Type = EventType.KeyPress, KeyCode = code },
            new() { Type = EventType.KeyRelease, KeyCode = code, DelayMs = delay, Timestamp = delay },
        },
    }, path);

    private MacroPlayer CreatePlayer()
    {
        var simulator = Substitute.For<IInputSimulator>();
        simulator.IsSupported.Returns(returnThis: true);
        simulator.When(s => s.KeyPress(Arg.Any<int>(), Arg.Any<bool>())).Do(call =>
        {
            var code = call.ArgAt<int>(0);
            var down = call.ArgAt<bool>(1);
            _output.Enqueue((code, down));
            if (code is 248 && down)
            {
                _ = _pressSeen.TrySetResult();
            }
        });
        return new MacroPlayer(new PlaybackValidator(_mapper, provider: null), new MacroPlayerDependencies(
            positionProvider: null, timingService: new SystemPlaybackTimingService(), playbackWaitAsync: WaitAsync,
            playbackElapsedMillisecondsFactory: () => { double elapsed = 0; return () => elapsed += 10; },
            coordinatorFactory: () => new DefaultPlaybackCoordinator(positionProvider: null),
            buttonTrackerFactory: () => new ButtonStateTracker(), keyTrackerFactory: () => new KeyStateTracker(),
            buttonMapper: new DefaultPlaybackMouseButtonMapper(), inputSimulatorFactory: () => simulator,
            simulatorPool: null, screenPixelReader: NullScreenPixelReader.Instance,
            keyCodeMapper: _mapper, windowManager: new NullWindowManager(), clipboardService: null,
            shellCommandRunner: null, screenshotCaptureService: null,
            imageClickMovementResolver: new ImageClickMovementResolver(mousePositionProvider: null),
            imageAssetCodec: new ImageAssetCodec(), delayResolver: new PlaybackDelayResolver()));
    }

    private async Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (_pausePress && _pressSeen.Task.IsCompleted && delay > TimeSpan.Zero && Interlocked.Increment(ref _waits) is 1)
        {
            await _continuePress.Task.WaitAsync(cancellationToken);
        }
    }

    public void Dispose()
    {
        _continuePress.TrySetResult();
        _service.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private sealed class CleanupGatePlayer(MacroPlayer inner, TaskCompletionSource entered, ManualResetEventSlim resume) : IMacroPlayer
    {
        public bool IsPlaying => inner.IsPlaying;
        public bool IsPaused => inner.IsPaused;
        public int CurrentLoop => inner.CurrentLoop;
        public int TotalLoops => inner.TotalLoops;
        public bool IsWaitingBetweenLoops => inner.IsWaitingBetweenLoops;
        public Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default) => inner.PlayAsync(macro, options, cancellationToken);
        public void Pause() => inner.Pause();
        public void ResumePlayback() => inner.ResumePlayback();
        public void StopPlayback()
        {
            entered.TrySetResult();
            if (!resume.Wait(Timeout)) { throw new TimeoutException("Player cleanup was not released."); }
            inner.StopPlayback();
        }
        public void Dispose() => inner.Dispose();
    }
}
