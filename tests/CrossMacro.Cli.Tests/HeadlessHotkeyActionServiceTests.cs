
namespace CrossMacro.Cli.Tests;

public sealed class HeadlessHotkeyActionServiceTests
{
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task RestartedSession_StopsAndUnsubscribesItsOwnHotkeys()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var player = Substitute.For<IMacroPlayer>();
        await using var service = new HeadlessHotkeyActionService(
            hotkeys,
            Substitute.For<IMacroRecorder>(),
            () => player,
            CreateSettings(new AppSettings()));

        for (var session = 0; session < 2; session++)
        {
            service.Start();
            Assert.True(service.IsRunning);
            await service.StopAsync(CancellationToken.None);
            Assert.False(service.IsRunning);
        }

        hotkeys.Received(2).ToggleRecordingRequested += Arg.Any<EventHandler>();
        hotkeys.Received(2).ToggleRecordingRequested -= Arg.Any<EventHandler>();
        hotkeys.Received(2).TogglePlaybackRequested += Arg.Any<EventHandler>();
        hotkeys.Received(2).TogglePlaybackRequested -= Arg.Any<EventHandler>();
        hotkeys.Received(2).TogglePauseRequested += Arg.Any<EventHandler>();
        hotkeys.Received(2).TogglePauseRequested -= Arg.Any<EventHandler>();
    }

    [Fact]
    public async Task RecordingHotkeyToggle_StartsThenStopsRecording()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var recording = CreateToggleRecorder(KeyPressMacro());
        var player = Substitute.For<IMacroPlayer>();
        await using var service = new HeadlessHotkeyActionService(hotkeys, recording.Recorder, () => player, CreateSettings(RecordingSettings()));
        service.Start();

        await RecordMacroAsync(hotkeys, recording);

        _ = recording.Recorder.Received(1).StartRecordingAsync(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IEnumerable<int>>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        _ = recording.Recorder.Received(1).StopRecording();
        hotkeys.Received(1).SetPlaybackPauseHotkeysEnabled(enabled: false);
        hotkeys.Received(1).SetPlaybackPauseHotkeysEnabled(enabled: true);
    }

    [Fact]
    public async Task PlaybackHotkeyToggle_WithNoRecordedMacro_DoesNotStartPlayer()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var recorder = Substitute.For<IMacroRecorder>();
        _ = recorder.IsRecording.Returns(returnThis: false);
        var player = Substitute.For<IMacroPlayer>();
        var playerFactoryCalls = 0;
        await using var service = new HeadlessHotkeyActionService(hotkeys, recorder, () =>
        {
            playerFactoryCalls++;
            return player;
        }, CreateSettings(new AppSettings()));
        service.Start();

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(0, playerFactoryCalls);
        _ = player.DidNotReceive().PlayAsync(
            Arg.Any<MacroSequence>(),
            Arg.Any<PlaybackOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordingHotkeyToggle_StopsEvenWhenStartTaskDoesNotComplete()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var recording = CreateToggleRecorder(KeyPressMacro(), onStart: () => Task.Delay(Timeout.Infinite, CancellationToken.None));
        var player = Substitute.For<IMacroPlayer>();
        await using var service = new HeadlessHotkeyActionService(hotkeys, recording.Recorder, () => player, CreateSettings(RecordingSettings()));
        service.Start();

        await RecordMacroAsync(hotkeys, recording);

        _ = recording.Recorder.Received(1).StopRecording();
    }

    [Fact]
    public async Task PlaybackHotkeyToggle_StartsAndStopsPlayback_ForLastRecordedMacro()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var settings = CreateSettings(new AppSettings
        {
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true,
            PlaybackSpeed = 1.5,
            IsLooping = true,
            LoopCount = 3,
            LoopDelayMs = 10,
            MotionMode = MotionPlaybackMode.StrictSpeed,
            StrictSpeedMotionEventsPerSecond = 4_321,
            PrecisionMotionEventsPerSecond = 987,
            MaximumMotionErrorPixels = 7.5,
        });
        var recording = CreateToggleRecorder(ClickMacro());
        var playback = CreateBlockingPlayer();
        await using var service = new HeadlessHotkeyActionService(hotkeys, recording.Recorder, () => playback.Player, settings, new FakeTimeProvider());
        service.Start();
        await RecordMacroAsync(hotkeys, recording);

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await playback.PlayStarted.WaitAsync(SignalTimeout, CancellationToken.None);
        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await playback.StopCalled.WaitAsync(SignalTimeout, CancellationToken.None);

        var options = Assert.IsType<PlaybackOptions>(playback.CapturedOptions);
        Assert.Equal(1.5, options.SpeedMultiplier);
        Assert.True(options.Loop);
        Assert.Equal(3, options.RepeatCount);
        Assert.Equal(10, options.RepeatDelayMs);
        Assert.Equal(MotionPlaybackMode.StrictSpeed, options.MotionMode);
        Assert.Equal(4_321, options.StrictSpeedMotionEventsPerSecond);
        Assert.Equal(987, options.PrecisionMotionEventsPerSecond);
        Assert.Equal(7.5, options.MaximumMotionErrorPixels);
    }

    [Fact]
    public async Task PlaybackHotkeyToggle_ForwardsRandomLoopDelaySettings()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var settings = CreateSettings(new AppSettings
        {
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true,
            IsLooping = true,
            LoopCount = 2,
            UseRandomLoopDelay = true,
            LoopDelayMinMs = 40,
            LoopDelayMaxMs = 80,
        });
        var recording = CreateToggleRecorder(ClickMacro());
        var playback = CreateBlockingPlayer();
        await using var service = new HeadlessHotkeyActionService(hotkeys, recording.Recorder, () => playback.Player, settings);
        service.Start();
        await RecordMacroAsync(hotkeys, recording);

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await playback.PlayStarted.WaitAsync(SignalTimeout, CancellationToken.None);

        var options = Assert.IsType<PlaybackOptions>(playback.CapturedOptions);
        Assert.True(options.UseRandomRepeatDelay);
        Assert.Equal(40, options.RepeatDelayMinMs);
        Assert.Equal(80, options.RepeatDelayMaxMs);

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await playback.StopCalled.WaitAsync(SignalTimeout, CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_WaitsForActivePlaybackTaskToFinishCleanup()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var recording = CreateToggleRecorder(KeyPressMacro());
        var player = new GatedCleanupPlayer();
        await using var service = new HeadlessHotkeyActionService(hotkeys, recording.Recorder, () => player, CreateSettings(RecordingSettings()));
        service.Start();
        await RecordMacroAsync(hotkeys, recording);

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await player.PlayStarted.WaitAsync(SignalTimeout, CancellationToken.None);

        var stopTask = service.StopAsync(CancellationToken.None);
        _ = Assert.Throws<InvalidOperationException>(service.Start);
        player.AllowCleanupToComplete();
        await stopTask;

        Assert.True(player.PlayCompleted.IsSignaled);
        Assert.True(player.DisposeCalled);
        service.Start();
        Assert.True(service.IsRunning);
        await service.StopAsync(CancellationToken.None);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task Dispose_BeforeStart_ThenDisposeAsync_IsIdempotent()
    {
        var service = new HeadlessHotkeyActionService(
            Substitute.For<IGlobalHotkeyService>(),
            Substitute.For<IMacroRecorder>(),
            () => Substitute.For<IMacroPlayer>(),
            CreateSettings(new AppSettings()));

        service.Dispose();
        await service.DisposeAsync();

        Assert.False(service.IsRunning);
        _ = Assert.Throws<ObjectDisposedException>(service.Start);
    }

    [Fact]
    public async Task DisposeAsync_AfterStop_WaitsForPendingStopCleanupBeforeDisposingGate()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var recording = CreateToggleRecorder(ClickMacro());
        var player = new GatedCleanupPlayer();
        var service = new HeadlessHotkeyActionService(hotkeys, recording.Recorder, () => player, CreateSettings(RecordingSettings()));
        service.Start();
        await RecordMacroAsync(hotkeys, recording);

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await player.PlayStarted.WaitAsync(SignalTimeout, CancellationToken.None);

        var stopTask = service.StopAsync(CancellationToken.None);
        await player.StopCalled.WaitAsync(SignalTimeout, CancellationToken.None);
        await player.CleanupEntered.WaitAsync(SignalTimeout, CancellationToken.None);

        var disposeTask = service.DisposeAsync().AsTask();

        Assert.False(disposeTask.IsCompleted);
        Assert.False(player.DisposeCalled);

        player.AllowCleanupToComplete();
        await disposeTask.WaitAsync(SignalTimeout, TimeProvider.System, CancellationToken.None);
        await stopTask;

        Assert.True(player.PlayCompleted.IsSignaled);
        Assert.True(player.DisposeCalled);
    }

    [Fact]
    public async Task RecordingHotkeyToggle_ForwardsForceRelativeSettings()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var settings = CreateSettings(new AppSettings
        {
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true,
            ForceRelativeCoordinates = true,
            SkipInitialZeroZero = true,
        });
        var recording = CreateToggleRecorder(KeyPressMacro());
        var player = Substitute.For<IMacroPlayer>();
        await using var service = new HeadlessHotkeyActionService(hotkeys, recording.Recorder, () => player, settings);
        service.Start();

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await recording.StartRequested.WaitAsync(SignalTimeout, CancellationToken.None);

        await recording.Recorder.Received(1).StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            Arg.Any<IEnumerable<int>>(),
            forceRelative: true,
            skipInitialZero: true,
            useLogicalRelativeCoordinates: false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordingHotkeyToggle_WhenLogicalRelativeEnabled_ForwardsLogicalChoice()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var settings = CreateSettings(new AppSettings
        {
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true,
            ForceRelativeCoordinates = true,
            UseLogicalRelativeCoordinates = true,
        });
        var recording = CreateToggleRecorder(KeyPressMacro());
        var player = Substitute.For<IMacroPlayer>();
        await using var service = new HeadlessHotkeyActionService(hotkeys, recording.Recorder, () => player, settings);
        service.Start();

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await recording.StartRequested.WaitAsync(SignalTimeout, CancellationToken.None);

        await recording.Recorder.Received(1).StartRecordingAsync(
            recordMouse: true,
            recordKeyboard: true,
            Arg.Any<IEnumerable<int>>(),
            forceRelative: true,
            skipInitialZero: false,
            useLogicalRelativeCoordinates: true,
            Arg.Any<CancellationToken>());
    }

    private static ISettingsService CreateSettings(AppSettings settings)
    {
        var substitute = SettingsServiceSubstitute.Create();
        _ = substitute.Current.Returns(settings);
        return substitute;
    }

    private static AppSettings RecordingSettings() => new()
    {
        IsMouseRecordingEnabled = true,
        IsKeyboardRecordingEnabled = true,
    };

    private static MacroSequence KeyPressMacro() => new()
    {
        Events = { new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 } },
    };

    private static MacroSequence ClickMacro() => new()
    {
        Events = { new MacroEvent { Type = EventType.Click, Button = MacroMouseButton.Left } },
    };

    private static ToggleRecording CreateToggleRecorder(MacroSequence recorded, Func<Task>? onStart = null)
    {
        var isRecording = false;
        var startRequested = new AsyncSignal();
        var stopRequested = new AsyncSignal();
        var recorder = Substitute.For<IMacroRecorder>();
        _ = recorder.IsRecording.Returns(_ => isRecording);
        _ = recorder.StartRecordingAsync(Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IEnumerable<int>>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                isRecording = true;
                startRequested.Signal();
                return onStart?.Invoke() ?? Task.CompletedTask;
            });
        _ = recorder.StopRecording().Returns(_ =>
        {
            isRecording = false;
            stopRequested.Signal();
            return recorded;
        });

        return new ToggleRecording(recorder, startRequested, stopRequested);
    }

    private static async Task RecordMacroAsync(IGlobalHotkeyService hotkeys, ToggleRecording recording)
    {
        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await recording.StartRequested.WaitAsync(SignalTimeout, CancellationToken.None);
        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await recording.StopRequested.WaitAsync(SignalTimeout, CancellationToken.None);
    }

    private static BlockingPlayback CreateBlockingPlayer()
    {
        var playback = new BlockingPlayback();
        _ = playback.Player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                playback.CapturedOptions = call.ArgAt<PlaybackOptions>(1);
                playback.PlayStarted.Signal();
                return Task.Delay(Timeout.Infinite, call.ArgAt<CancellationToken>(2));
            });
        playback.Player.When(player => player.StopPlayback()).Do(_ => playback.StopCalled.Signal());
        return playback;
    }

    private sealed record ToggleRecording(IMacroRecorder Recorder, AsyncSignal StartRequested, AsyncSignal StopRequested);

    private sealed class BlockingPlayback
    {
        public IMacroPlayer Player { get; } = Substitute.For<IMacroPlayer>();
        public AsyncSignal PlayStarted { get; } = new();
        public AsyncSignal StopCalled { get; } = new();
        public PlaybackOptions? CapturedOptions { get; set; }
    }

    private sealed class GatedCleanupPlayer : IMacroPlayer
    {
        private readonly TaskCompletionSource _allowCleanupToComplete = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AsyncSignal PlayStarted { get; } = new();
        public AsyncSignal StopCalled { get; } = new();
        public AsyncSignal CleanupEntered { get; } = new();
        public AsyncSignal PlayCompleted { get; } = new();
        public bool DisposeCalled { get; private set; }

        public bool IsPlaying => false;
        public bool IsPaused => false;
        public int CurrentLoop => 1;
        public int TotalLoops => 1;
        public bool IsWaitingBetweenLoops => false;

        public async Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default)
        {
            PlayStarted.Signal();

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                CleanupEntered.Signal();
                await _allowCleanupToComplete.Task;
                PlayCompleted.Signal();
            }
        }

        public void AllowCleanupToComplete()
        {
            _allowCleanupToComplete.SetResult();
        }

        public void StopPlayback()
        {
            StopCalled.Signal();
        }

        public void Pause()
        {
        }

        public void ResumePlayback()
        {
        }

        public void Dispose()
        {
            DisposeCalled = true;
        }
    }
}
