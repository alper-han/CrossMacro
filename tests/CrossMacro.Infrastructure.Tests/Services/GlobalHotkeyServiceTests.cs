
using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class GlobalHotkeyServiceTests : IDisposable
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private static readonly string[] ExpectedUpdateHotkeys = ["F3"];

    private readonly IHotkeyConfigurationService _config;
    private readonly ModifierStateTracker _modifierTracker = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly IInputCapture _inputCapture;
    private readonly GlobalHotkeyService _service;

    [Fact]
    public void RawHotkeyInputEventArgs_NullPressedModifiers_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new RawHotkeyInputEventArgs(1, null!, "F1"));
    }

    [Fact]
    public void RawHotkeyInputEventArgs_NullHotkeyString_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new RawHotkeyInputEventArgs(1, new HashSet<int>(), null!));
    }

    public GlobalHotkeyServiceTests()
    {
        _config = Substitute.For<IHotkeyConfigurationService>();
        _ = _config.Load().Returns(new HotkeySettings
        {
            RecordingHotkey = "F9",
            PlaybackHotkey = "F10",
            PauseHotkey = "F11",
        });

        _inputCapture = Substitute.For<IInputCapture>();

        _ = _config.CaptureSaveRequest(Arg.Any<HotkeySettings>())
            .Returns(call => new HotkeyConfigurationSaveRequest(
                "test-hotkeys.json",
                call.Arg<HotkeySettings>().RecordingHotkey,
                call.Arg<HotkeySettings>().PlaybackHotkey,
                call.Arg<HotkeySettings>().PauseHotkey));
        _ = _config.TrySaveAsync(Arg.Any<HotkeyConfigurationSaveRequest>()).Returns(Task.FromResult(true));

        _service = CreateService(() => _inputCapture);
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    private GlobalHotkeyService CreateService(Func<IInputCapture> inputCaptureFactory) =>
        CreateService(_config, inputCaptureFactory, _modifierTracker, _timeProvider);

    private static GlobalHotkeyService CreateService(
        IHotkeyConfigurationService config,
        Func<IInputCapture> inputCaptureFactory,
        ModifierStateTracker modifierTracker,
        TimeProvider timeProvider)
    {
        var layoutService = Substitute.For<IKeyboardLayoutService>();
        _ = layoutService.GetKeyName(Arg.Any<int>()).Returns(call =>
            string.Create(CultureInfo.InvariantCulture, $"Key{call.Arg<int>()}"));
        var keyCodeMapper = new KeyCodeMapper(layoutService);

        return new GlobalHotkeyService(
            config,
            new HotkeyParser(keyCodeMapper),
            new HotkeyMatcher(timeProvider),
            modifierTracker,
            new HotkeyStringBuilder(keyCodeMapper),
            inputCaptureFactory,
            timeProvider: timeProvider);
    }

    [Fact]
    public async Task DefaultAsyncAdapters_HonorCancellationAndDelegateSynchronousStop()
    {
        IGlobalHotkeyService service = new SynchronousOnlyHotkeyService();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await service.InitializeAsync(cancellation.Token));
        _ = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await service.StopHotkeyServiceAsync(cancellation.Token));

        await service.StopHotkeyServiceAsync(CancellationToken.None);

        Assert.True(((SynchronousOnlyHotkeyService)service).WasStopped);
    }

    [Fact]
    public void Start_InitializesInputCapture()
    {
        // Act
        _service.Start();

        // Assert
        _inputCapture.Received(1).Configure(captureMouse: true, captureKeyboard: true);
        Assert.True(_service.IsRunning);
    }

    [Fact]
    public void Start_WhenCalledTwice_DoesNotCreateOrStartSecondCapture()
    {
        _service.Start();

        _service.Start();

        _inputCapture.Received(1).Configure(captureMouse: true, captureKeyboard: true);
        _ = _inputCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CaptureNextKeyAsync_WhenServiceNotRunning_FailsFast()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CaptureNextKeyAsync(CancellationToken.None));

        Assert.Contains("not running", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CaptureNextKeyAsync_WhenServiceHasUnavailableBackendError_FailsFast()
    {
        var capture = Substitute.For<IInputCapture>();
        _ = capture.ProviderName.Returns("unavailable");
        _ = capture.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("No usable Linux input capture backend is available.")));

        var service = CreateService(() => capture);

        service.Start();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CaptureNextKeyAsync(CancellationToken.None));

        Assert.Contains("No usable Linux input capture backend is available", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No usable Linux input capture backend is available", service.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CaptureNextKeyAsync_WhenCancelled_DoesNotSurfaceError()
    {
        _service.Start();

        using var cts = new CancellationTokenSource();
        var captureTask = _service.CaptureNextKeyAsync(cts.Token);
        await cts.CancelAsync();

        _ = await Assert.ThrowsAsync<TaskCanceledException>(async () => await captureTask);
        Assert.Null(_service.LastError);
    }

    [Fact]
    public async Task CaptureNextKeyAsync_WhenServiceStops_CompletesAsCanceled()
    {
        _service.Start();
        var captureTask = _service.CaptureNextKeyAsync(CancellationToken.None);

        await _service.StopHotkeyServiceAsync(CancellationToken.None);

        _ = await Assert.ThrowsAsync<TaskCanceledException>(async () => await captureTask);
    }

    [Fact]
    public void Stop_WhenCalledTwice_IsIdempotent()
    {
        _service.Start();

        _service.StopHotkeyService();
        _service.StopHotkeyService();

        _inputCapture.Received(1).StopCapture();
        _inputCapture.Received(1).Dispose();
        Assert.False(_service.IsRunning);
    }

    [Fact]
    public void OnInputReceived_TriggersRecordingEvent_WhenHotkeyMatch()
    {
        // Arrange
        _service.Start();
        bool eventFired = false;
        _service.ToggleRecordingRequested += (s, e) => eventFired = true;

        // Simulate F9 Press (Code 67, Value 1)
        var args = new CapturedInputEvent { Type = InputEventType.Key, Code = 67, Value = 1 };

        // Use NSubstitute to raise event
        _inputCapture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(this, new CapturedInputEventArgs(args));

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void OnInputReceived_DoesNotTrigger_WhenNoMatch()
    {
        // Arrange
        _service.Start();
        bool eventFired = false;
        _service.ToggleRecordingRequested += (s, e) => eventFired = true;

        // Simulate F8 Press (Code 66, Value 1)
        var args = new CapturedInputEvent { Type = InputEventType.Key, Code = 66, Value = 1 };
        _inputCapture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(this, new CapturedInputEventArgs(args));

        // Assert
        Assert.False(eventFired);
    }

    [Fact]
    public void OnInputReceived_MouseButtonPress_RaisesRawInputReceived()
    {
        // Arrange
        _service.Start();
        RawHotkeyInputEventArgs? received = null;
        _service.RawInputReceived += (_, e) => received = e;

        // Act
        var args = new CapturedInputEvent
        {
            Type = InputEventType.MouseButton,
            Code = InputEventCode.BTN_EXTRA,
            Value = 1,
        };
        _inputCapture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(this, new CapturedInputEventArgs(args));

        // Assert
        Assert.NotNull(received);
        Assert.Equal(InputEventCode.BTN_EXTRA, received.KeyCode);
        Assert.Equal("Mouse Extra", received.HotkeyString);
    }

    [Fact]
    public void OnInputReceived_MouseButtonRelease_RaisesRawKeyReleased()
    {
        // Arrange
        _service.Start();
        RawHotkeyInputEventArgs? released = null;
        _service.RawKeyReleased += (_, e) => released = e;

        // Act
        var args = new CapturedInputEvent
        {
            Type = InputEventType.MouseButton,
            Code = InputEventCode.BTN_EXTRA,
            Value = 0,
        };
        _inputCapture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(this, new CapturedInputEventArgs(args));

        // Assert
        Assert.NotNull(released);
        Assert.Equal(InputEventCode.BTN_EXTRA, released.KeyCode);
        Assert.Equal(string.Empty, released.HotkeyString);
    }

    [Fact]
    public void UpdateHotkeys_Parses_Correctly()
    {
        // Act
        _service.UpdateHotkeys("F7", "F10", "F11");

        // Assert
        Assert.Equal(InputEventCode.KEY_F7, _service.RecordingHotkeyCode);
    }

    [Fact]
    public void UpdateHotkeys_CoalescesPendingSaves_AndDisposeFlushesLatestState()
    {
        var saved = new List<string>();
        _ = _config.TrySaveAsync(Arg.Any<HotkeyConfigurationSaveRequest>())
            .Returns(call =>
            {
                saved.Add(call.Arg<HotkeyConfigurationSaveRequest>().RecordingHotkey);
                return true;
            });

        _service.UpdateHotkeys("F1", "F10", "F11");
        _service.UpdateHotkeys("F2", "F10", "F11");
        _service.UpdateHotkeys("F3", "F10", "F11");

        Assert.Empty(saved);

        _service.Dispose();

        Assert.Equal(ExpectedUpdateHotkeys, saved, StringComparer.Ordinal);
    }

    [Fact]
    public async Task PendingSave_RetainsCapturedProfilePathAfterReload()
    {
        var currentPath = "profile-a/hotkeys.json";
        var saveStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSave = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        HotkeyConfigurationSaveRequest? request = null;

        _ = _config.CaptureSaveRequest(Arg.Any<HotkeySettings>())
            .Returns(call => new HotkeyConfigurationSaveRequest(
                currentPath,
                call.Arg<HotkeySettings>().RecordingHotkey,
                call.Arg<HotkeySettings>().PlaybackHotkey,
                call.Arg<HotkeySettings>().PauseHotkey));
        _ = _config.TrySaveAsync(Arg.Any<HotkeyConfigurationSaveRequest>())
            .Returns(call =>
            {
                request = call.Arg<HotkeyConfigurationSaveRequest>();
                _ = saveStarted.TrySetResult(true);
                _ = allowSave.Task.GetAwaiter().GetResult();
                return true;
            });

        _service.UpdateHotkeys("F1", "F10", "F11");
        _timeProvider.Advance(TimeSpan.FromSeconds(3));
        _ = await saveStarted.Task.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);
        currentPath = "profile-b/hotkeys.json";
        _ = allowSave.TrySetResult(true);

        _service.Dispose();

        Assert.NotNull(request);
        Assert.Equal("profile-a/hotkeys.json", request.ConfigPath);
    }

    [Fact]
    public async Task SaveFailure_SetsLastErrorAndRaisesErrorOccurred()
    {
        var observed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _config.TrySaveAsync(Arg.Any<HotkeyConfigurationSaveRequest>()).Returns(Task.FromResult(false));
        _service.ErrorOccurred += (_, error) => observed.TrySetResult(error.Message);

        _service.UpdateHotkeys("F1", "F10", "F11");
        _timeProvider.Advance(TimeSpan.FromSeconds(3));
        var error = await observed.Task.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);
        _service.Dispose();

        Assert.Contains("Failed to save hotkey configuration", error, StringComparison.Ordinal);
        Assert.Equal(error, _service.LastError);
    }

    [Fact]
    public void OnInputReceived_PlaybackHotkeyF9FromMacNormalizedInput_TriggersPlaybackExactlyOnce()
    {
        var service = CreateRealMatcherService(recordingHotkey: "F8", playbackHotkey: "F9", pauseHotkey: "F10", out var capture);
        service.Start();
        var playbackRequests = 0;
        service.TogglePlaybackRequested += (_, _) => playbackRequests++;

        capture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
            this,
                new CapturedInputEventArgs
                {
                    Type = InputEventType.Key,
                    Code = InputEventCode.KEY_F9,
                    Value = 1,
                    Timestamp = 123,
                });

        Assert.Equal(1, playbackRequests);
    }

    [Theory]
    [InlineData(InputEventCode.KEY_F10)]
    [InlineData(InputEventCode.KEY_F21)]
    [InlineData(0)]
    [InlineData(InputEventCode.KEY_PLAYPAUSE)]
    public void OnInputReceived_PlaybackHotkeyF9_DoesNotTriggerForNonF9UnsupportedUnknownOrMediaKeys(int inputCode)
    {
        var service = CreateRealMatcherService(recordingHotkey: "F8", playbackHotkey: "F9", pauseHotkey: "F10", out var capture);
        service.Start();
        var playbackRequests = 0;
        service.TogglePlaybackRequested += (_, _) => playbackRequests++;

        capture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
            this,
                new CapturedInputEventArgs
                {
                    Type = InputEventType.Key,
                    Code = inputCode,
                    Value = 1,
                    Timestamp = 123,
                });

        Assert.Equal(0, playbackRequests);
    }

    private static GlobalHotkeyService CreateRealMatcherService(
        string recordingHotkey,
        string playbackHotkey,
        string pauseHotkey,
        out IInputCapture capture)
    {
        var config = Substitute.For<IHotkeyConfigurationService>();
        _ = config.Load().Returns(new HotkeySettings
        {
            RecordingHotkey = recordingHotkey,
            PlaybackHotkey = playbackHotkey,
            PauseHotkey = pauseHotkey,
        });

        var inputCapture = Substitute.For<IInputCapture>();
        capture = inputCapture;

        return CreateService(config, () => inputCapture, new ModifierStateTracker(), new FakeTimeProvider());
    }

    [Fact]
    public async Task OnInputCaptureError_ShouldAttemptSingleRestart()
    {
        var firstCapture = Substitute.For<IInputCapture>();
        var secondCapture = Substitute.For<IInputCapture>();
        _ = firstCapture.ProviderName.Returns("first");
        _ = secondCapture.ProviderName.Returns("second");
        _ = firstCapture.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var secondStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = secondCapture.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => secondStarted.TrySetResult(true));

        var factoryCall = 0;
        var restartingService = CreateService(() => ++factoryCall is 1 ? firstCapture : secondCapture);

        restartingService.Start();

        firstCapture.CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(this, new InputCaptureErrorEventArgs("simulated capture error"));
        _timeProvider.Advance(TimeSpan.FromMilliseconds(250));
        _ = await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, CancellationToken.None);

        firstCapture.Received(1).StopCapture();
        firstCapture.Received(1).Dispose();
        secondCapture.Received(1).Configure(captureMouse: true, captureKeyboard: true);
        await secondCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());

        Received.InOrder(() =>
        {
            firstCapture.StopCapture();
            firstCapture.Dispose();
            secondCapture.Configure(captureMouse: true, captureKeyboard: true);
            _ = secondCapture.StartAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task OnInputCaptureError_WhenUInputInitializationFails_ShouldNotRestart()
    {
        var capture = Substitute.For<IInputCapture>();
        _ = capture.ProviderName.Returns("daemon");
        _ = capture.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var factoryCalls = 0;
        var errorObserved = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateService(() =>
            {
                factoryCalls++;
                return capture;
            });
        service.ErrorOccurred += (_, error) => errorObserved.TrySetResult(error.Message);

        service.Start();
        const string message = "Failed to init UInput: Cannot open /dev/uinput (Errno: 13). Permission denied.";
        capture.CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(
            this,
            new InputCaptureErrorEventArgs(message));

        Assert.Equal(message, await errorObserved.Task.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None));
        Assert.Equal(1, factoryCalls);
        Assert.Equal(message, service.LastError);
    }

    [Fact]
    public async Task OnInputCaptureError_WhenStartTaskJustCompleted_ShouldStillRestart()
    {
        var firstCapture = Substitute.For<IInputCapture>();
        var secondCapture = Substitute.For<IInputCapture>();
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = firstCapture.ProviderName.Returns("first");
        _ = secondCapture.ProviderName.Returns("second");
        _ = firstCapture.StartAsync(Arg.Any<CancellationToken>()).Returns(firstStarted.Task);
        _ = secondCapture.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => secondStarted.TrySetResult(true));

        var factoryCall = 0;
        var restartingService = CreateService(() => ++factoryCall is 1 ? firstCapture : secondCapture);

        restartingService.Start();

        _ = firstStarted.TrySetResult(true);
        firstCapture.CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(this, new InputCaptureErrorEventArgs("transient post-start failure"));
        _timeProvider.Advance(TimeSpan.FromMilliseconds(250));

        _ = await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, CancellationToken.None);

        Assert.True(restartingService.IsRunning);
        firstCapture.Received(1).StopCapture();
        firstCapture.Received(1).Dispose();
        secondCapture.Received(1).Configure(captureMouse: true, captureKeyboard: true);
        await secondCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnRecoveryInputCaptureError_RestartsCaptureClearsModifiersAndDoesNotNotifyUser()
    {
        var firstCapture = Substitute.For<IInputCapture>();
        var secondCapture = Substitute.For<IInputCapture>();
        var secondStarted = new AsyncSignal();
        var notified = false;

        _ = firstCapture.ProviderName.Returns("first");
        _ = secondCapture.ProviderName.Returns("second");
        _ = firstCapture.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _ = secondCapture.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => secondStarted.Signal());

        var factoryCall = 0;
        var restartingService = CreateService(() => ++factoryCall is 1 ? firstCapture : secondCapture);

        restartingService.ErrorOccurred += (_, _) => notified = true;

        restartingService.Start();
        _modifierTracker.OnKeyPressed(InputEventCode.KEY_LEFTCTRL);
        firstCapture.CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(
            this,
            new InputCaptureErrorEventArgs("Recovery: Windows session unlocked; restarting input capture."));
        _timeProvider.Advance(TimeSpan.FromMilliseconds(250));

        await secondStarted.WaitAsync(TestTimeout, CancellationToken.None);

        Assert.False(notified);
        Assert.False(_modifierTracker.HasModifiers);
        firstCapture.Received(1).StopCapture();
        firstCapture.Received(1).Dispose();
        secondCapture.Received(1).Configure(captureMouse: true, captureKeyboard: true);
        await secondCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenCaptureStartTaskFaults_ShouldStopWithoutRestart()
    {
        var firstCapture = Substitute.For<IInputCapture>();
        var startupFaultObserved = new AsyncSignal();
        _ = firstCapture.ProviderName.Returns("first");
        _ = firstCapture.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("startup failed")));

        var factoryCallCount = 0;

        var restartingService = CreateService(() =>
            {
                factoryCallCount++;
                return firstCapture;
            });

        restartingService.ErrorOccurred += (_, error) =>
        {
            if (error.Message is "startup failed")
            {
                startupFaultObserved.Signal();
            }
        };

        restartingService.Start();

        await startupFaultObserved.WaitAsync(TestTimeout, CancellationToken.None);

        Assert.False(restartingService.IsRunning);
        Assert.Equal(1, factoryCallCount);
        Assert.Equal("startup failed", restartingService.LastError);
        firstCapture.Received(1).StopCapture();
        firstCapture.Received(1).Dispose();
        await firstCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restart_WhenReplacementCaptureStartThrows_CleansUpFailedCaptureAndStopsService()
    {
        var firstCapture = Substitute.For<IInputCapture>();
        var secondCapture = Substitute.For<IInputCapture>();
        var restartFailureObserved = new AsyncSignal();
        _ = firstCapture.ProviderName.Returns("first");
        _ = secondCapture.ProviderName.Returns("second");
        _ = firstCapture.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        secondCapture
            .When(x => x.StartAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("replacement failed"));

        var factoryCall = 0;
        var restartingService = CreateService(() => ++factoryCall is 1 ? firstCapture : secondCapture);

        restartingService.ErrorOccurred += (_, error) =>
        {
            if (error.Message.Contains("Restart failed", StringComparison.OrdinalIgnoreCase))
            {
                restartFailureObserved.Signal();
            }
        };

        restartingService.Start();

        firstCapture.CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(this, new InputCaptureErrorEventArgs("simulated capture error"));
        _timeProvider.Advance(TimeSpan.FromMilliseconds(250));
        await restartFailureObserved.WaitAsync(TestTimeout, CancellationToken.None);

        Assert.False(restartingService.IsRunning);
        Assert.Contains("Restart failed", restartingService.LastError, StringComparison.OrdinalIgnoreCase);
        firstCapture.Received(1).StopCapture();
        firstCapture.Received(1).Dispose();
        secondCapture.Received(1).Configure(captureMouse: true, captureKeyboard: true);
        await secondCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
        secondCapture.Received(1).StopCapture();
        secondCapture.Received(1).Dispose();

        Received.InOrder(() =>
        {
            firstCapture.StopCapture();
            firstCapture.Dispose();
            secondCapture.Configure(captureMouse: true, captureKeyboard: true);
            _ = secondCapture.StartAsync(Arg.Any<CancellationToken>());
            secondCapture.StopCapture();
            secondCapture.Dispose();
        });

        await restartingService.StopHotkeyServiceAsync(CancellationToken.None);
    }

    [Fact]
    public void Dispose_WhileARestartIsWaiting_CancelsTheRestart()
    {
        var firstCapture = Substitute.For<IInputCapture>();
        _ = firstCapture.ProviderName.Returns("first");
        _ = firstCapture.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var factoryCalls = 0;
        var restartingService = CreateService(() =>
        {
            factoryCalls++;
            return firstCapture;
        });
        restartingService.Start();
        firstCapture.CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(
            this,
            new InputCaptureErrorEventArgs("simulated capture error"));

        restartingService.Dispose();
        _timeProvider.Advance(TimeSpan.FromMilliseconds(250));

        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task CaptureNextKeyAsync_WhenAModifiedMouseButtonIsPressed_ReturnsTheCanonicalChord()
    {
        _service.Start();
        var capture = _service.CaptureNextKeyAsync(CancellationToken.None);
        _modifierTracker.OnKeyPressed(InputEventCode.KEY_LEFTCTRL);

        _inputCapture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
            this,
            new CapturedInputEventArgs(new CapturedInputEvent
            {
                Type = InputEventType.MouseButton,
                Code = InputEventCode.BTN_EXTRA,
                Value = 1,
            }));

        Assert.Equal("Ctrl+Mouse Extra", await capture);
    }

    [Fact]
    public void OnInputReceived_BareLeftClick_IsIgnored()
    {
        _service.Start();
        var raised = false;
        _service.RawInputReceived += (_, _) => raised = true;

        _inputCapture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
            this,
            new CapturedInputEventArgs(new CapturedInputEvent
            {
                Type = InputEventType.MouseButton,
                Code = InputEventCode.BTN_LEFT,
                Value = 1,
            }));

        Assert.False(raised);
    }

    private sealed class SynchronousOnlyHotkeyService : IGlobalHotkeyService
    {
        public int RecordingHotkeyCode => -1;
        public int PlaybackHotkeyCode => -1;
        public int PauseHotkeyCode => -1;
        public bool IsRunning => false;
        public bool WasStopped { get; private set; }
        public string? LastError => null;

        public event EventHandler? ToggleRecordingRequested
        {
            add { }
            remove { }
        }

        public event EventHandler? TogglePlaybackRequested
        {
            add { }
            remove { }
        }

        public event EventHandler? TogglePauseRequested
        {
            add { }
            remove { }
        }

        public event EventHandler<RawHotkeyInputEventArgs>? RawInputReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<RawHotkeyInputEventArgs>? RawKeyReleased
        {
            add { }
            remove { }
        }

        public event EventHandler<GlobalHotkeyErrorEventArgs>? ErrorOccurred
        {
            add { }
            remove { }
        }

        public void Start() { }
        public void StopHotkeyService() => WasStopped = true;
        public void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey) { }
        public void ApplyHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey) { }
        public Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public void SetPlaybackPauseHotkeysEnabled(bool enabled) { }
        public void Dispose() { }
    }
}
