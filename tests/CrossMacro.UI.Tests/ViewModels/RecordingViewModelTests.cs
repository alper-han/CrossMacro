
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class RecordingViewModelTests : IDisposable
{
    private readonly IMacroRecorder _recorder;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly IRuntimeContext _runtimeContext;
    private readonly RecordingViewModel _viewModel;

    public RecordingViewModelTests()
    {
        _recorder = Substitute.For<IMacroRecorder>();
        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
        _settingsService = Substitute.For<ISettingsService>();
        _localizationService = Substitute.For<ILocalizationService>();
        _runtimeContext = Substitute.For<IRuntimeContext>();
        _ = _runtimeContext.IsLinux.Returns(returnThis: true);
        _ = _localizationService["Recording_StatusReady"].Returns("[Recording_StatusReady]");
        _ = _localizationService["Recording_StatusRecording"].Returns("[Recording_StatusRecording]");
        _ = _localizationService["Recording_StatusLoadedEvents"].Returns("[Recording_StatusLoadedEvents] {0}");
        _ = _localizationService["Recording_StatusRecordedEvents"].Returns("[Recording_StatusRecordedEvents] {0}");
        _ = _localizationService["Recording_StatusError"].Returns("[Recording_StatusError] {0}");
        _ = _localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));

        // Setup default settings
        _ = _settingsService.Current.Returns(new AppSettings
        {
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true,
        });
        _ = _settingsService.SaveAfterIdleAsync().Returns(Task.CompletedTask);

        _viewModel = new RecordingViewModel(
            _recorder,
            _hotkeyService,
            _settingsService,
            _localizationService,
            _runtimeContext);
    }

    public void Dispose()
    {
        _viewModel.Dispose();
    }

    [Fact]
    public void Constructor_InitializesPropertiesFromSettings()
    {
        // Assert
        Assert.True(_viewModel.IsMouseRecordingEnabled);
        Assert.True(_viewModel.IsKeyboardRecordingEnabled);
        Assert.False(_viewModel.IsRecording);
        Assert.Equal("[Recording_StatusReady]", _viewModel.RecordingStatus);
    }

    [Fact]
    public async Task StartRecordingAsync_WhileRecorderStartIsPending_DefersRecordingState()
    {
        // Arrange
        using var viewModel = CreateViewModel(action => action());
        viewModel.CanStartRecordingExternal = true;
        var startCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _recorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(startCompletion.Task);

        var recordingStateChangedCount = 0;
        var recordingStateChangedValue = false;
        viewModel.RecordingStateChanged += (_, isRecording) =>
        {
            recordingStateChangedCount++;
            recordingStateChangedValue = isRecording;
        };

        // Act
        var startTask = viewModel.StartRecordingAsync();

        // Assert while pending
        Assert.False(startTask.IsCompleted);
        Assert.False(viewModel.IsRecording);
        Assert.Equal("[Recording_StatusReady]", viewModel.RecordingStatus);
        Assert.False(viewModel.CanStartRecording);
        Assert.False(viewModel.CanToggleRecording);
        Assert.False(viewModel.ToggleRecordingCommand.CanExecute(parameter: null));
        Assert.Equal(0, recordingStateChangedCount);

        PublishRecordedEvent(new MacroEvent { Type = EventType.MouseMove });
        Assert.Equal(0, viewModel.EventCount);
        Assert.Equal(0, viewModel.MouseEventCount);
        Assert.Equal(0, viewModel.KeyboardEventCount);

        startCompletion.SetResult(true);
        await startTask;

        // Assert
        Assert.True(viewModel.IsRecording);
        Assert.Equal("[Recording_StatusRecording]", viewModel.RecordingStatus);
        Assert.True(viewModel.CanToggleRecording);
        Assert.Equal(1, recordingStateChangedCount);
        Assert.True(recordingStateChangedValue);

        PublishRecordedEvent(new MacroEvent { Type = EventType.MouseMove });
        Assert.Equal(1, viewModel.EventCount);
        Assert.Equal(1, viewModel.MouseEventCount);
        Assert.Equal(0, viewModel.KeyboardEventCount);

        await _recorder.Received(1).StartRecordingAsync(
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IEnumerable<int>>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(enabled: false);
        _hotkeyService.DidNotReceive().SetPlaybackPauseHotkeysEnabled(enabled: true);
    }

    [Fact]
    public async Task StartRecordingAsync_WhenRecorderStartCompletes_SetsRecordingStateAndActivatesLiveCounters()
    {
        // Arrange
        using var viewModel = CreateViewModel(action => action());
        viewModel.CanStartRecordingExternal = true;
        var startCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _recorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(startCompletion.Task);

        var recordingStateChangedCount = 0;
        viewModel.RecordingStateChanged += (_, isRecording) =>
        {
            if (isRecording)
            {
                recordingStateChangedCount++;
            }
        };

        // Act
        var startTask = viewModel.StartRecordingAsync();
        startCompletion.SetResult(true);
        await startTask;

        // Assert
        Assert.True(viewModel.IsRecording);
        Assert.Equal("[Recording_StatusRecording]", viewModel.RecordingStatus);
        Assert.False(viewModel.CanStartRecording);
        Assert.True(viewModel.CanToggleRecording);
        Assert.True(viewModel.ToggleRecordingCommand.CanExecute(parameter: null));
        Assert.Equal(1, recordingStateChangedCount);

        PublishRecordedEvent(new MacroEvent { Type = EventType.KeyPress });
        Assert.Equal(1, viewModel.EventCount);
        Assert.Equal(0, viewModel.MouseEventCount);
        Assert.Equal(1, viewModel.KeyboardEventCount);

        await _recorder.Received(1).StartRecordingAsync(
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IEnumerable<int>>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(enabled: false);
        _hotkeyService.DidNotReceive().SetPlaybackPauseHotkeysEnabled(enabled: true);
    }

    [Fact]
    public async Task StartRecordingAsync_WhenCannotStartExternal_DoesNotStart()
    {
        // Arrange
        _viewModel.CanStartRecordingExternal = false;

        // Act
        await _viewModel.StartRecordingAsync();

        // Assert
        Assert.False(_viewModel.IsRecording);
        await _recorder.DidNotReceive().StartRecordingAsync(
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int[]>());
    }

    [Fact]
    public async Task StopRecording_WhenRecording_StopsAndReturnsMacro()
    {
        // Arrange
        var expectedMacro = new MacroSequence();
        expectedMacro.Events.Add(new MacroEvent { Type = EventType.MouseMove });
        _ = _recorder.StopRecording().Returns(expectedMacro);

        await _viewModel.StartRecordingAsync();

        // Act
        var result = _viewModel.StopRecording();

        // Assert
        Assert.False(_viewModel.IsRecording); // Should be false after stop
        Assert.Equal(expectedMacro, result);
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(enabled: true);
    }

    [Fact]
    public async Task StopRecording_WhenRecordingCompletedHandlerThrows_DoesNotConvertSuccessToError()
    {
        // Arrange
        var expectedMacro = new MacroSequence();
        expectedMacro.Events.Add(new MacroEvent { Type = EventType.MouseMove });
        _ = _recorder.StopRecording().Returns(expectedMacro);
        _viewModel.RecordingCompleted += (_, _) => throw new InvalidOperationException("handler failure");
        await _viewModel.StartRecordingAsync();

        // Act
        var result = _viewModel.StopRecording();

        // Assert
        Assert.Equal(expectedMacro, result);
        Assert.Equal("[Recording_StatusRecordedEvents] 1", _viewModel.RecordingStatus);
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(enabled: true);
    }

    [Fact]
    public async Task CultureChanged_AfterRecordedMacro_PreservesRecordedStatus()
    {
        var recordedMacro = new MacroSequence
        {
            Events =
            {
                new MacroEvent { Type = EventType.MouseMove },
                new MacroEvent { Type = EventType.KeyPress },
            },
        };
        _ = _recorder.StopRecording().Returns(recordedMacro);
        await _viewModel.StartRecordingAsync();

        _ = _viewModel.StopRecording();

        _ = _localizationService["Recording_StatusRecordedEvents"].Returns("[Recording_StatusRecordedEvents:tr] {0}");
        _ = _localizationService["Recording_StatusLoadedEvents"].Returns("[Recording_StatusLoadedEvents:tr] {0}");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        Assert.Equal("[Recording_StatusRecordedEvents:tr] 2", _viewModel.RecordingStatus);
    }

    [Fact]
    public async Task StopRecording_WhenQueuedLiveCounterUpdateArrivesLater_IgnoresStaleUpdate()
    {
        var recordedMacro = new MacroSequence
        {
            Events =
            {
                new MacroEvent { Type = EventType.MouseMove },
                new MacroEvent { Type = EventType.KeyPress },
            },
        };
        _ = _recorder.StopRecording().Returns(recordedMacro);
        var queuedCallbacks = new Queue<Action>();
        using var viewModel = CreateViewModel(queuedCallbacks.Enqueue);
        viewModel.CanStartRecordingExternal = true;

        await viewModel.StartRecordingAsync();

        PublishRecordedEvent(new MacroEvent { Type = EventType.MouseMove });
        Assert.Single(queuedCallbacks);

        var stoppedMacro = viewModel.StopRecording();
        Assert.Same(recordedMacro, stoppedMacro);
        while (queuedCallbacks.Count > 0)
        {
            var queuedCallback = queuedCallbacks.Dequeue();
            queuedCallback();
        }

        Assert.Equal(2, viewModel.EventCount);
        Assert.Equal(1, viewModel.MouseEventCount);
        Assert.Equal(1, viewModel.KeyboardEventCount);
        Assert.Equal("[Recording_StatusRecordedEvents] 2", viewModel.RecordingStatus);
    }

    [Fact]
    public async Task LiveCounters_WhenBurstArrives_CoalescesIntoOnePostWithExactCategories()
    {
        var queuedCallbacks = new Queue<Action>();
        using var viewModel = CreateViewModel(queuedCallbacks.Enqueue);
        await viewModel.StartRecordingAsync();

        PublishRecordedEvent(new MacroEvent { Type = EventType.MouseMove });
        PublishRecordedEvent(new MacroEvent { Type = EventType.KeyPress });
        PublishRecordedEvent(new MacroEvent { Type = EventType.Click });
        PublishRecordedEvent(new MacroEvent { Type = EventType.KeyRelease });

        Assert.Single(queuedCallbacks);

        queuedCallbacks.Dequeue()();

        Assert.Equal(4, viewModel.EventCount);
        Assert.Equal(2, viewModel.MouseEventCount);
        Assert.Equal(2, viewModel.KeyboardEventCount);
    }

    [Fact]
    public async Task LiveCounters_WhenEventArrivesAfterDrain_SchedulesNewPost()
    {
        var queuedCallbacks = new Queue<Action>();
        using var viewModel = CreateViewModel(queuedCallbacks.Enqueue);
        await viewModel.StartRecordingAsync();

        PublishRecordedEvent(new MacroEvent { Type = EventType.MouseMove });
        queuedCallbacks.Dequeue()();

        PublishRecordedEvent(new MacroEvent { Type = EventType.KeyPress });

        Assert.Single(queuedCallbacks);

        queuedCallbacks.Dequeue()();

        Assert.Equal(2, viewModel.EventCount);
        Assert.Equal(1, viewModel.MouseEventCount);
        Assert.Equal(1, viewModel.KeyboardEventCount);
    }

    [Fact]
    public async Task LiveCounters_WhenEventCountSubscriberThrows_DrainReleasesSchedulingOwnership()
    {
        var queuedCallbacks = new Queue<Action>();
        using var viewModel = CreateViewModel(queuedCallbacks.Enqueue);
        await viewModel.StartRecordingAsync();

        PropertyChangedEventHandler throwingHandler = (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(RecordingViewModel.EventCount), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("subscriber failed");
            }
        };
        viewModel.PropertyChanged += throwingHandler;

        PublishRecordedEvent(new MacroEvent { Type = EventType.MouseMove });
        queuedCallbacks.Dequeue()();

        viewModel.PropertyChanged -= throwingHandler;
        PublishRecordedEvent(new MacroEvent { Type = EventType.KeyPress });

        Assert.Single(queuedCallbacks);
        queuedCallbacks.Dequeue()();

        Assert.Equal(2, viewModel.EventCount);
        Assert.Equal(1, viewModel.MouseEventCount);
        Assert.Equal(1, viewModel.KeyboardEventCount);
    }

    [Fact]
    public async Task LiveCounters_WhenConcurrentProducersPublishDuringDrainRelease_DoNotLoseOrStallUpdates()
    {
        var callbackCollector = new CallbackCollector();
        using var viewModel = CreateViewModel(callbackCollector.Post);
        await viewModel.StartRecordingAsync();

        var drainReachedCounterApplication = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDrain = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (!string.Equals(args.PropertyName, nameof(RecordingViewModel.EventCount), StringComparison.Ordinal))
            {
                return;
            }

            if (!drainReachedCounterApplication.TrySetResult(true))
            {
                return;
            }

            releaseDrain.Task.GetAwaiter().GetResult();
        };

        PublishRecordedEvent(new MacroEvent { Type = EventType.MouseMove });
        Assert.Equal(1, callbackCollector.QueuedCount);

        var drainTask = Task.Run(callbackCollector.ExecuteNext, CancellationToken.None);
        await drainReachedCounterApplication.Task;

        var producers = Enumerable.Range(0, 128)
            .Select(_ => Task.Run(
                () => PublishRecordedEvent(new MacroEvent { Type = EventType.KeyPress }),
                CancellationToken.None));
        await Task.WhenAll(producers);
        Assert.Equal(0, callbackCollector.QueuedCount);

        _ = releaseDrain.TrySetResult(true);
        await drainTask;

        Assert.Equal(1, callbackCollector.QueuedCount);
        Assert.Equal(1, callbackCollector.MaximumQueuedCount);

        callbackCollector.ExecuteNext();

        Assert.Equal(129, viewModel.EventCount);
        Assert.Equal(1, viewModel.MouseEventCount);
        Assert.Equal(128, viewModel.KeyboardEventCount);
        Assert.Equal(0, callbackCollector.QueuedCount);
        Assert.Equal(1, callbackCollector.MaximumQueuedCount);
    }

    [Fact]
    public async Task LiveCounters_WhenPostCallbackThrows_ReleasesSchedulingOwnershipForLaterEvents()
    {
        var callbackCollector = new CallbackCollector();
        Action<Action> postCallback = _ => throw new InvalidOperationException("post failed");
        using var viewModel = CreateViewModel(callback => postCallback(callback));
        await viewModel.StartRecordingAsync();

        Assert.Throws<InvalidOperationException>(() => PublishRecordedEvent(new MacroEvent { Type = EventType.MouseMove }));

        postCallback = callbackCollector.Post;
        PublishRecordedEvent(new MacroEvent { Type = EventType.KeyPress });

        Assert.Equal(1, callbackCollector.QueuedCount);
        callbackCollector.ExecuteNext();

        Assert.Equal(2, viewModel.EventCount);
        Assert.Equal(1, viewModel.MouseEventCount);
        Assert.Equal(1, viewModel.KeyboardEventCount);
        Assert.Equal(0, callbackCollector.QueuedCount);
    }

    [Fact]
    public async Task LiveCounters_WhenSessionAIsStale_CannotConsumeOrUpdateSessionB()
    {
        var queuedCallbacks = new Queue<Action>();
        using var viewModel = CreateViewModel(queuedCallbacks.Enqueue);
        var firstMacro = new MacroSequence
        {
            Events = { new MacroEvent { Type = EventType.MouseMove } },
        };
        var secondMacro = new MacroSequence
        {
            Events = { new MacroEvent { Type = EventType.KeyPress } },
        };
        _ = _recorder.StopRecording().Returns(firstMacro, secondMacro);

        await viewModel.StartRecordingAsync();
        PublishRecordedEvent(new MacroEvent { Type = EventType.MouseMove });
        viewModel.StopRecording();

        await viewModel.StartRecordingAsync();
        PublishRecordedEvent(new MacroEvent { Type = EventType.KeyPress });

        var staleSessionCallback = queuedCallbacks.Dequeue();
        staleSessionCallback();

        Assert.Equal(0, viewModel.EventCount);
        Assert.Single(queuedCallbacks);

        queuedCallbacks.Dequeue()();

        Assert.Equal(1, viewModel.EventCount);
        Assert.Equal(0, viewModel.MouseEventCount);
        Assert.Equal(1, viewModel.KeyboardEventCount);
        viewModel.StopRecording();
    }

    [Fact]
    public async Task LiveCounters_WhenDisposed_QueuedCallbackDoesNothing()
    {
        var queuedCallbacks = new Queue<Action>();
        var viewModel = CreateViewModel(queuedCallbacks.Enqueue);
        await viewModel.StartRecordingAsync();
        PublishRecordedEvent(new MacroEvent { Type = EventType.MouseMove });

        viewModel.Dispose();
        queuedCallbacks.Dequeue()();

        Assert.Equal(0, viewModel.EventCount);
        Assert.Equal(0, viewModel.MouseEventCount);
        Assert.Equal(0, viewModel.KeyboardEventCount);
    }

    [Fact]
    public async Task StopRecording_WhenMacroEventsCollectionIsNull_DoesNotThrowOrSetErrorStatus()
    {
        // Arrange
        _ = _recorder.StopRecording().Returns(new MacroSequence());
        await _viewModel.StartRecordingAsync();

        // Act
        var result = _viewModel.StopRecording();

        // Assert
        Assert.Null(result);
        Assert.Equal("[Recording_StatusReady]", _viewModel.RecordingStatus);
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(enabled: true);
    }

    [Fact]
    public void StopRecording_WhenNotRecording_ReturnsNull()
    {
        // Act
        var result = _viewModel.StopRecording();

        // Assert
        Assert.Null(result);
        _ = _recorder.DidNotReceive().StopRecording();
    }

    [Fact]
    public async Task ToggleRecording_WhenRecording_Stops()
    {
        // Arrange
        _ = _recorder.StopRecording().Returns(new MacroSequence());
        await _viewModel.StartRecordingAsync();

        // Act
        _viewModel.ToggleRecording();

        // Assert
        Assert.False(_viewModel.IsRecording);
        _ = _recorder.Received(1).StopRecording();
    }

    [Fact]
    public void ToggleRecording_WhenNotRecording_Starts()
    {
        // Arrange
        _viewModel.CanStartRecordingExternal = true;
        // Act
        _viewModel.ToggleRecording();

        // Assert
        Assert.True(_viewModel.IsRecording);
    }

    [Fact]
    public void ToggleRecordingCommand_WhenCannotToggle_DoesNotStart()
    {
        // Arrange
        _viewModel.IsMouseRecordingEnabled = false;
        _viewModel.IsKeyboardRecordingEnabled = false;

        // Act
        var canExecute = _viewModel.ToggleRecordingCommand.CanExecute(parameter: null);
        _viewModel.ToggleRecordingCommand.Execute(parameter: null);

        // Assert
        Assert.False(canExecute);
        Assert.False(_viewModel.IsRecording);
        _ = _recorder.DidNotReceiveWithAnyArgs().StartRecordingAsync(default!, default!, default!);
    }

    [Fact]
    public async Task ToggleRecordingCommand_WhenRecording_CanExecuteEvenIfStartingIsDisabled()
    {
        // Arrange
        await _viewModel.StartRecordingAsync();
        _viewModel.CanStartRecordingExternal = false;
        _viewModel.IsMouseRecordingEnabled = false;
        _viewModel.IsKeyboardRecordingEnabled = false;

        // Assert
        Assert.True(_viewModel.ToggleRecordingCommand.CanExecute(parameter: null));
    }

    [Fact]
    public async Task StartRecordingAsync_WhenRecorderThrows_LogsAndExposesLocalizedErrorAndReenablesHotkeys()
    {
        // Arrange
        _viewModel.CanStartRecordingExternal = true;
        var startException = new InvalidOperationException("start failed");
        var startCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _recorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(startCompletion.Task);
        var logger = Substitute.For<CrossMacro.Core.Logging.ICoreLogger>();
        using var loggingScope = CrossMacro.Core.Logging.Log.PushLogger(logger);

        // Act
        var startTask = _viewModel.StartRecordingAsync();

        Assert.False(_viewModel.IsRecording);
        Assert.Equal("[Recording_StatusReady]", _viewModel.RecordingStatus);
        Assert.False(_viewModel.CanStartRecording);
        Assert.False(_viewModel.CanToggleRecording);

        startCompletion.SetException(startException);
        await startTask;

        // Assert
        Assert.False(_viewModel.IsRecording);
        Assert.Equal("[Recording_StatusError] start failed", _viewModel.RecordingStatus);
        Assert.True(_viewModel.CanStartRecording);
        Assert.True(_viewModel.CanToggleRecording);
        Assert.True(_viewModel.ToggleRecordingCommand.CanExecute(parameter: null));
        PublishRecordedEvent(new MacroEvent { Type = EventType.MouseMove });
        Assert.Equal(0, _viewModel.EventCount);
        logger.Received(1).LogError(
            startException,
            Arg.Any<string>(),
            Arg.Any<object?[]>());
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(enabled: false);
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(enabled: true);
    }

    [Fact]
    public async Task StartRecordingAsync_UsesRelativeRecordingSettings()
    {
        // Arrange
        _viewModel.CanStartRecordingExternal = true;
        _viewModel.ForceRelativeCoordinates = true;
        _viewModel.SkipInitialZeroZero = true;

        // Act
        await _viewModel.StartRecordingAsync();

        // Assert
        await _recorder.Received(1).StartRecordingAsync(
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IEnumerable<int>>(),
            _viewModel.ForceRelativeCoordinates,
            _viewModel.SkipInitialZeroZero,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void LogicalRelativeCoordinates_WhenGlobalCursorPositionIsUnavailable_IsDisabled()
    {
        _viewModel.ForceRelativeCoordinates = true;

        Assert.True(_viewModel.ShowLogicalRelativeCoordinatesOption);
        Assert.False(_viewModel.IsLogicalRelativeCoordinatesAvailable);

        _viewModel.UseLogicalRelativeCoordinates = true;

        Assert.True(_viewModel.UseLogicalRelativeCoordinates);
        Assert.True(_settingsService.Current.UseLogicalRelativeCoordinates);
    }

    [Fact]
    public void LogicalRelativeCoordinates_WhenPositionProviderBecomesAvailable_EnablesWithoutClearingPreference()
    {
        var positionProvider = new NotifyingPositionProvider(isAvailable: false);
        var settings = new AppSettings
        {
            ForceRelativeCoordinates = true,
            UseLogicalRelativeCoordinates = true,
        };
        var settingsService = Substitute.For<ISettingsService>();
        _ = settingsService.Current.Returns(settings);
        _ = settingsService.SaveAfterIdleAsync().Returns(Task.CompletedTask);
        using var viewModel = new RecordingViewModel(
            _recorder,
            _hotkeyService,
            settingsService,
            _localizationService,
            _runtimeContext,
            positionProvider);

        Assert.False(viewModel.IsLogicalRelativeCoordinatesAvailable);
        Assert.True(viewModel.UseLogicalRelativeCoordinates);

        positionProvider.PublishPosition(100, 200);

        Assert.True(viewModel.IsLogicalRelativeCoordinatesAvailable);
        Assert.True(viewModel.UseLogicalRelativeCoordinates);
    }

    [Fact]
    public async Task StartRecordingAsync_WhenLogicalRelativeIsAvailable_ForwardsLogicalChoice()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.SupportsAbsolutePosition.Returns(returnThis: true);
        using var viewModel = new RecordingViewModel(
            _recorder,
            _hotkeyService,
            _settingsService,
            _localizationService,
            _runtimeContext,
            positionProvider);
        viewModel.ForceRelativeCoordinates = true;
        viewModel.UseLogicalRelativeCoordinates = true;

        await viewModel.StartRecordingAsync();

        Assert.True(viewModel.IsLogicalRelativeCoordinatesAvailable);
        await _recorder.Received(1).StartRecordingAsync(
            Arg.Is(true),
            Arg.Is(true),
            Arg.Any<IEnumerable<int>>(),
            Arg.Is(true),
            Arg.Is(false),
            Arg.Is(true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_WhenRuntimeDoesNotSupportForceRelative_DisablesSetting()
    {
        var settingsService = Substitute.For<ISettingsService>();
        _ = settingsService.Current.Returns(new AppSettings
        {
            ForceRelativeCoordinates = true,
        });

        var runtimeContext = Substitute.For<IRuntimeContext>();
        _ = runtimeContext.IsLinux.Returns(returnThis: false);
        _ = runtimeContext.IsWindows.Returns(returnThis: false);
        _ = runtimeContext.IsMacOS.Returns(returnThis: false);

        var viewModel = new RecordingViewModel(
            _recorder,
            _hotkeyService,
            settingsService,
            _localizationService,
            runtimeContext);

        Assert.False(viewModel.IsForceRelativeSupported);
        Assert.False(viewModel.ForceRelativeCoordinates);
    }

    [Fact]
    public void Constructor_WhenRuntimeIsMacOS_SupportsForceRelativeSetting()
    {
        var settingsService = Substitute.For<ISettingsService>();
        _ = settingsService.Current.Returns(new AppSettings
        {
            ForceRelativeCoordinates = true,
        });

        var runtimeContext = Substitute.For<IRuntimeContext>();
        _ = runtimeContext.IsLinux.Returns(returnThis: false);
        _ = runtimeContext.IsWindows.Returns(returnThis: false);
        _ = runtimeContext.IsMacOS.Returns(returnThis: true);

        var viewModel = new RecordingViewModel(
            _recorder,
            _hotkeyService,
            settingsService,
            _localizationService,
            runtimeContext);

        Assert.True(viewModel.IsForceRelativeSupported);
        Assert.True(viewModel.ForceRelativeCoordinates);
    }

    [Fact]
    public void IsMouseRecordingEnabled_WhenSaveFails_RollsBackValue()
    {
        _ = _settingsService.SaveAfterIdleAsync().Returns(Task.FromException(new InvalidOperationException("disk full")));

        _viewModel.IsMouseRecordingEnabled = false;

        Assert.True(_viewModel.IsMouseRecordingEnabled);
        Assert.True(_settingsService.Current.IsMouseRecordingEnabled);
    }

    [Fact]
    public void IsKeyboardRecordingEnabled_WhenSaveFails_RollsBackValueAndCommandAvailability()
    {
        _ = _settingsService.SaveAfterIdleAsync().Returns(Task.FromException(new InvalidOperationException("disk full")));

        _viewModel.IsKeyboardRecordingEnabled = false;

        Assert.True(_viewModel.IsKeyboardRecordingEnabled);
        Assert.True(_settingsService.Current.IsKeyboardRecordingEnabled);
        Assert.True(_viewModel.CanToggleRecording);
        Assert.True(_viewModel.ToggleRecordingCommand.CanExecute(parameter: null));
    }

    [Fact]
    public async Task StopRecording_WhenRecorderThrows_ReturnsNullAndResetsState()
    {
        // Arrange
        _ = _recorder.StopRecording().Returns(_ => throw new InvalidOperationException("stop failed"));
        await _viewModel.StartRecordingAsync();

        // Act
        var result = _viewModel.StopRecording();

        // Assert
        Assert.Null(result);
        Assert.False(_viewModel.IsRecording);
        Assert.Equal("[Recording_StatusReady]", _viewModel.RecordingStatus);
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(enabled: true);
    }

    [Fact]
    public void SetMacro_UpdatesEventCountersByType()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Events =
            {
                new MacroEvent { Type = EventType.MouseMove },
                new MacroEvent { Type = EventType.ButtonPress },
                new MacroEvent { Type = EventType.KeyPress },
                new MacroEvent { Type = EventType.KeyRelease },
            },
        };

        // Act
        _viewModel.SetMacro(macro);

        // Assert
        Assert.Equal(4, _viewModel.EventCount);
        Assert.Equal(2, _viewModel.MouseEventCount);
        Assert.Equal(2, _viewModel.KeyboardEventCount);
        Assert.Equal("[Recording_StatusLoadedEvents] 4", _viewModel.RecordingStatus);
    }

    [Fact]
    public void SetMacro_WhenNull_ClearsEventCountersAndResetsStatus()
    {
        var macro = new MacroSequence
        {
            Events =
            {
                new MacroEvent { Type = EventType.MouseMove },
                new MacroEvent { Type = EventType.KeyPress },
            },
        };

        _viewModel.SetMacro(macro);
        _viewModel.SetMacro(macro: null);

        Assert.Equal(0, _viewModel.EventCount);
        Assert.Equal(0, _viewModel.MouseEventCount);
        Assert.Equal(0, _viewModel.KeyboardEventCount);
        Assert.Equal("[Recording_StatusReady]", _viewModel.RecordingStatus);
    }

    private RecordingViewModel CreateViewModel(Action<Action> postCallback)
    {
        return new RecordingViewModel(
            _recorder,
            _hotkeyService,
            _settingsService,
            _localizationService,
            _runtimeContext,
            postCallback);
    }

    private void PublishRecordedEvent(MacroEvent macroEvent)
    {
        _recorder.EventRecorded += Raise.Event<EventHandler<MacroEventRecordedEventArgs>>(
            _recorder,
            new MacroEventRecordedEventArgs(macroEvent));
    }

    private sealed class CallbackCollector
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _callbacks = new();
        private int _queuedCount;
        private int _maximumQueuedCount;

        public int QueuedCount => Volatile.Read(ref _queuedCount);

        public int MaximumQueuedCount => Volatile.Read(ref _maximumQueuedCount);

        public void Post(Action callback)
        {
            _callbacks.Enqueue(callback);
            var queuedCount = Interlocked.Increment(ref _queuedCount);
            while (true)
            {
                var maximumQueuedCount = Volatile.Read(ref _maximumQueuedCount);
                if (maximumQueuedCount >= queuedCount ||
                    Interlocked.CompareExchange(ref _maximumQueuedCount, queuedCount, maximumQueuedCount) == maximumQueuedCount)
                {
                    return;
                }
            }
        }

        public void ExecuteNext()
        {
            Assert.True(_callbacks.TryDequeue(out var callback));
            _ = Interlocked.Decrement(ref _queuedCount);
            callback();
        }
    }

    private sealed class NotifyingPositionProvider(bool isAvailable) : IMousePositionProvider, IMousePositionAvailability, IMousePositionChangeSource
    {
        private bool _isAvailable = isAvailable;

        public string ProviderName => "test";
        public bool IsSupported => true;
        public bool IsPositionAvailable => _isAvailable;
        public event EventHandler<MousePositionChangedEventArgs>? PositionChanged;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync() => Task.FromResult<(int X, int Y)?>(null);

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult<(int Width, int Height)?>(null);

        public void PublishPosition(int x, int y)
        {
            _isAvailable = true;
            PositionChanged?.Invoke(this, new MousePositionChangedEventArgs(x, y, isDiscontinuity: false));
        }

        public void Dispose() { /* Test provider has no resources. */ }
    }
}
