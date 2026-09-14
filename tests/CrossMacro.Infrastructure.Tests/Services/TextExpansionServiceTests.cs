
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class TextExpansionServiceTests : IDisposable
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    private readonly ISettingsService _settingsService;
    private readonly ITextExpansionStorageService _storageService;
    private readonly IInputCapture _inputCapture;

    // New Mocks
    private readonly IInputProcessor _inputProcessor;
    private readonly ITextBufferState _bufferState;
    private readonly ITextExpansionExecutor _executor;

    private readonly TextExpansionService _service;

    public TextExpansionServiceTests()
    {
        _settingsService = CrossMacro.Tests.SettingsServiceSubstitute.Create();
        _ = _settingsService.Current.Returns(new AppSettings { EnableTextExpansion = true });

        _storageService = Substitute.For<ITextExpansionStorageService>();
        _inputCapture = Substitute.For<IInputCapture>();

        _inputProcessor = Substitute.For<IInputProcessor>();
        _bufferState = Substitute.For<ITextBufferState>();
        _executor = Substitute.For<ITextExpansionExecutor>();

        _service = new TextExpansionService(
            _settingsService,
            _storageService,
            () => _inputCapture,
            _inputProcessor,
            _bufferState,
            _executor);
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public async Task Start_WhenEnabled_StartsInputCaptureAndResetsState()
    {
        // Act
        _service.Start();

        // Assert
        Assert.True(_service.IsRunning);
        _ = _storageService.Received(1).Load();
        _inputCapture.Received(1).Configure(captureMouse: false, captureKeyboard: true);
        await _inputCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());

        _inputProcessor.Received(1).Reset();
        _bufferState.Received(1).Clear();
    }

    [Fact]
    public async Task Start_WhenCalledTwice_DoesNotCreateOrStartSecondCapture()
    {
        _service.Start();

        _service.Start();

        _ = _storageService.Received(1).Load();
        _inputCapture.Received(1).Configure(captureMouse: false, captureKeyboard: true);
        await _inputCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
        _inputProcessor.Received(1).Reset();
        _bufferState.Received(1).Clear();
    }

    [Fact]
    public async Task StartAsync_LoadsStorageBeforeStartingCapture()
    {
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _storageService.When(storage => storage.LoadAsync()).Do(_ => loadStarted.TrySetResult());
        _ = _storageService.LoadAsync().Returns(_ => allowLoad.Task.ContinueWith(
            _ => (IList<TextExpansionEntry>)[],
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default));

        var startTask = _service.StartAsync(CancellationToken.None);
        await loadStarted.Task.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        Assert.False(_service.IsRunning);
        await _inputCapture.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());

        allowLoad.SetResult();
        await startTask;

        Assert.True(_service.IsRunning);
        _ = _storageService.Received(1).LoadAsync();
        await _inputCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenFeatureIsDisabledDuringLoad_DoesNotActivateAndCanRetry()
    {
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _storageService.When(storage => storage.LoadAsync()).Do(_ => loadStarted.TrySetResult());
        _ = _storageService.LoadAsync().Returns(_ => allowLoad.Task.ContinueWith(
            _ => (IList<TextExpansionEntry>)[],
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default));

        var startTask = _service.StartAsync(CancellationToken.None);
        await loadStarted.Task.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);
        _ = _settingsService.Current.Returns(new AppSettings { EnableTextExpansion = false });

        allowLoad.SetResult();
        await startTask;

        Assert.False(_service.IsRunning);
        await _inputCapture.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());

        _ = _settingsService.Current.Returns(new AppSettings { EnableTextExpansion = true });
        await _service.StartAsync(CancellationToken.None);

        Assert.True(_service.IsRunning);
        await _inputCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenStoppedDuringLoad_DoesNotStartCapture()
    {
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _storageService.When(storage => storage.LoadAsync()).Do(_ => loadStarted.TrySetResult());
        _ = _storageService.LoadAsync().Returns(_ => allowLoad.Task.ContinueWith(
            _ => (IList<TextExpansionEntry>)[],
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default));

        var startTask = _service.StartAsync(CancellationToken.None);
        await loadStarted.Task.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        _service.StopExpansion();
        allowLoad.SetResult();
        await startTask;

        Assert.False(_service.IsRunning);
        await _inputCapture.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenCancelledDuringLoad_DoesNotStartCapture()
    {
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _storageService.When(storage => storage.LoadAsync()).Do(_ => loadStarted.TrySetResult());
        _ = _storageService.LoadAsync().Returns(_ => allowLoad.Task.ContinueWith(
            _ => (IList<TextExpansionEntry>)[],
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default));
        using var cancellation = new CancellationTokenSource();

        var startTask = _service.StartAsync(cancellation.Token);
        await loadStarted.Task.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        await cancellation.CancelAsync();
        allowLoad.SetResult();

        await TestAssertions.ThrowsAsync<OperationCanceledException>(() => startTask);
        Assert.False(_service.IsRunning);
        await _inputCapture.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenCancelledBeforeActivation_DoesNotStartCapture()
    {
        using var cancellation = new CancellationTokenSource();
        var settingsReadCount = 0;
        _ = _settingsService.Current.Returns(_ =>
        {
            if (Interlocked.Increment(ref settingsReadCount) is 2)
            {
                cancellation.Cancel();
            }

            return new AppSettings { EnableTextExpansion = true };
        });
        _ = _storageService.LoadAsync().Returns(Task.FromResult<IList<TextExpansionEntry>>([]));

        await TestAssertions.ThrowsAsync<OperationCanceledException>(() => _service.StartAsync(cancellation.Token));

        Assert.False(_service.IsRunning);
        await _inputCapture.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenOutOfMemoryOccurs_ClearsStartupStateBeforeRethrowing()
    {
#pragma warning disable CA2201 // The test deliberately verifies the runtime-reserved exception boundary.
        _ = _storageService.LoadAsync().Returns(Task.FromException<IList<TextExpansionEntry>>(new OutOfMemoryException()));
#pragma warning restore CA2201

        await TestAssertions.ThrowsAsync<OutOfMemoryException>(() => _service.StartAsync(CancellationToken.None));

        _ = _storageService.LoadAsync().Returns(Task.FromResult<IList<TextExpansionEntry>>([]));
        await _service.StartAsync(CancellationToken.None);

        Assert.True(_service.IsRunning);
        await _inputCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenCalledTwice_OnlyOneAttemptLoadsAndStarts()
    {
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _storageService.When(storage => storage.LoadAsync()).Do(_ => loadStarted.TrySetResult());
        _ = _storageService.LoadAsync().Returns(_ => allowLoad.Task.ContinueWith(
            _ => (IList<TextExpansionEntry>)[],
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default));

        var firstStartTask = _service.StartAsync(CancellationToken.None);
        await loadStarted.Task.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);
        var secondStartTask = _service.StartAsync(CancellationToken.None);

        Assert.True(secondStartTask.IsCompletedSuccessfully);
        allowLoad.SetResult();
        await firstStartTask;

        _ = _storageService.Received(1).LoadAsync();
        await _inputCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenDisabled_DoesNotStart()
    {
        // Arrange
        _ = _settingsService.Current.Returns(new AppSettings { EnableTextExpansion = false });

        // Act
        _service.Start();

        // Assert
        Assert.False(_service.IsRunning);
        _ = _storageService.DidNotReceive().Load();
        await _inputCapture.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Stop_StopsInputCapture()
    {
        // Arrange
        _service.Start();

        // Act
        _service.StopExpansion();

        // Assert
        _inputCapture.Received(1).StopCapture();
        _inputCapture.Received(1).Dispose();
    }

    [Fact]
    public void Stop_WhenCalledTwice_IsIdempotent()
    {
        _service.Start();

        _service.StopExpansion();
        _service.StopExpansion();

        _inputCapture.Received(1).StopCapture();
        _inputCapture.Received(1).Dispose();
        Assert.False(_service.IsRunning);
    }

    [Fact]
    public async Task StopExpansionAsync_WhenExpansionIsActive_WaitsForExpansionCompletion()
    {
        _service.Start();

        var expansion = new TextExpansionEntry { Trigger = ":a", Replacement = "alpha" };
        _ = _storageService.GetCurrent().Returns(new List<TextExpansionEntry> { expansion });
        _ = _bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansionEntry>>(), out Arg.Any<TextExpansionEntry?>())
            .Returns(callInfo =>
            {
                callInfo[1] = expansion;
                return true;
            });

        var expansionStarted = new AsyncSignal();
        var releaseExpansion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _executor.ExpandAsync(Arg.Any<TextExpansionEntry>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                expansionStarted.Signal();
                await releaseExpansion.Task;
            });

        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('a');
        await expansionStarted.WaitAsync(TestTimeout, CancellationToken.None);

        var stopTask = _service.StopExpansionAsync(CancellationToken.None);

        Assert.False(stopTask.IsCompleted);
        releaseExpansion.SetResult();
        await stopTask.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        Assert.False(_service.IsRunning);
    }

    [Fact]
    public async Task StopExpansionAsync_WhenCalledRepeatedly_WaitsForTheSameExpansionAndCleansUpOnce()
    {
        _service.Start();

        var expansion = new TextExpansionEntry { Trigger = ":a", Replacement = "alpha" };
        _ = _storageService.GetCurrent().Returns(new List<TextExpansionEntry> { expansion });
        _ = _bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansionEntry>>(), out Arg.Any<TextExpansionEntry?>())
            .Returns(callInfo =>
            {
                callInfo[1] = expansion;
                return true;
            });

        var expansionStarted = new AsyncSignal();
        var releaseExpansion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _executor.ExpandAsync(Arg.Any<TextExpansionEntry>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                expansionStarted.Signal();
                await releaseExpansion.Task;
            });

        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('a');
        await expansionStarted.WaitAsync(TestTimeout, CancellationToken.None);

        var firstStopTask = _service.StopExpansionAsync(CancellationToken.None);
        var secondStopTask = _service.StopExpansionAsync(CancellationToken.None);

        Assert.False(firstStopTask.IsCompleted);
        Assert.False(secondStopTask.IsCompleted);
        releaseExpansion.SetResult();
        await Task.WhenAll(firstStopTask, secondStopTask).WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        Assert.False(_service.IsRunning);
        _inputCapture.Received(1).StopCapture();
        _inputCapture.Received(1).Dispose();
    }

    [Fact]
    public async Task StartAsync_WhenStoppedAsynchronouslyDuringLoad_DoesNotStartCapture()
    {
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _storageService.When(storage => storage.LoadAsync()).Do(_ => loadStarted.TrySetResult());
        _ = _storageService.LoadAsync().Returns(_ => allowLoad.Task.ContinueWith(
            _ => (IList<TextExpansionEntry>)[],
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default));

        var startTask = _service.StartAsync(CancellationToken.None);
        await loadStarted.Task.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        var stopTask = _service.StopExpansionAsync(CancellationToken.None);
        await stopTask.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        Assert.False(_service.IsRunning);
        allowLoad.SetResult();
        await startTask.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        Assert.False(_service.IsRunning);
        await _inputCapture.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_AfterDispose_DoesNotRestartCapture()
    {
        _service.Start();
        _service.Dispose();

        _service.Start();

        Assert.False(_service.IsRunning);
        _ = _storageService.Received(1).Load();
        _inputCapture.Received(1).Configure(captureMouse: false, captureKeyboard: true);
        await _inputCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void OnInputReceived_DelegatesToProcessor()
    {
        // Arrange
        _service.Start();
        var eventArgs = new CapturedInputEvent { Type = InputEventType.Key, Code = 30, Value = 1 };

        // Act
        _inputCapture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(this, new CapturedInputEventArgs(eventArgs));

        // Assert
        _inputProcessor.Received(1).ProcessEvent(eventArgs);
    }

    [Fact]
    public async Task Expansion_WhenExecutorThrows_ExceptionIsHandledAndSubsequentExpansionStillRuns()
    {
        // Arrange
        _service.Start();

        var expansion = new TextExpansionEntry { Trigger = ":a", Replacement = "alpha" };
        _ = _storageService.GetCurrent().Returns(new List<TextExpansionEntry> { expansion });
        _ = _bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansionEntry>>(), out Arg.Any<TextExpansionEntry?>())
            .Returns(callInfo =>
            {
                callInfo[1] = expansion;
                return true;
            });

        var invocationCount = 0;
        var firstExpansionStarted = new AsyncSignal();
        var secondExpansionStarted = new AsyncSignal();
        _ = _executor.ExpandAsync(Arg.Any<TextExpansionEntry>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                invocationCount++;

                if (invocationCount is 1)
                {
                    firstExpansionStarted.Signal();
                }
                else if (invocationCount is 2)
                {
                    secondExpansionStarted.Signal();
                }

                return invocationCount is 1
                    ? Task.FromException(new InvalidOperationException("boom"))
                    : Task.CompletedTask;
            });

        // Act
        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('a');
        await firstExpansionStarted.WaitAsync(TestTimeout, CancellationToken.None);
        var firstExpansionTask = _service.ExpansionTask;
        Assert.NotNull(firstExpansionTask);
        await firstExpansionTask.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('a');

        // Assert
        await secondExpansionStarted.WaitAsync(TestTimeout, CancellationToken.None);
        await _executor.Received(2).ExpandAsync(Arg.Any<TextExpansionEntry>(), Arg.Any<CancellationToken>());
        Assert.True(_service.IsRunning);
    }

    [Fact]
    public async Task Expansion_WhenAlreadyRunning_DropsTriggersWithoutQueueingAndRecoversForLaterTrigger()
    {
        _service.Start();

        var expansion = new TextExpansionEntry { Trigger = ":a", Replacement = "alpha" };
        _ = _storageService.GetCurrent().Returns(new List<TextExpansionEntry> { expansion });
        _ = _bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansionEntry>>(), out Arg.Any<TextExpansionEntry?>())
            .Returns(callInfo =>
            {
                callInfo[1] = expansion;
                return true;
            });

        var firstStarted = new AsyncSignal();
        var firstFinished = new AsyncSignal();
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new AsyncSignal();
        var invocationCount = 0;
        _ = _executor.ExpandAsync(Arg.Any<TextExpansionEntry>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                if (Interlocked.Increment(ref invocationCount) is 1)
                {
                    firstStarted.Signal();
                    await releaseFirst.Task;
                    firstFinished.Signal();
                }
                else
                {
                    secondStarted.Signal();
                }
            });

        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('a');
        await firstStarted.WaitAsync(TestTimeout, CancellationToken.None);
        var firstExpansionTask = _service.ExpansionTask;
        Assert.NotNull(firstExpansionTask);

        for (var index = 0; index < 32; index++)
        {
            _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('a');
        }

        Assert.Equal(1, Volatile.Read(ref invocationCount));
        await _executor.Received(1).ExpandAsync(expansion, Arg.Any<CancellationToken>());

        releaseFirst.SetResult();
        await firstFinished.WaitAsync(TestTimeout, CancellationToken.None);
        await firstExpansionTask.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('a');
        await secondStarted.WaitAsync(TestTimeout, CancellationToken.None);

        Assert.Equal(2, Volatile.Read(ref invocationCount));
        await _executor.Received(2).ExpandAsync(expansion, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Expansion_WhenTriggerLastKeyIsStillPressed_WaitsForReleaseBeforeExecuting()
    {
        _service.Start();

        var expansion = new TextExpansionEntry { Trigger = ":test", Replacement = "done" };
        _ = _storageService.GetCurrent().Returns(new List<TextExpansionEntry> { expansion });
        _ = _bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansionEntry>>(), out Arg.Any<TextExpansionEntry?>())
            .Returns(callInfo =>
            {
                callInfo[1] = expansion;
                return true;
            });

        var expansionStarted = new AsyncSignal();
        _ = _executor.ExpandAsync(Arg.Any<TextExpansionEntry>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                expansionStarted.Signal();
                return Task.CompletedTask;
            });
        var triggerKeyReleaseWaitObserved = new AsyncSignal();
        var triggerKeyPressed = true;
        _ = _inputProcessor.IsKeyPressed(20).Returns(_ =>
        {
            triggerKeyReleaseWaitObserved.Signal();
            return triggerKeyPressed;
        });

        _inputCapture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
            this,
            new CapturedInputEventArgs { Type = InputEventType.Key, Code = 20, Value = 1 });
        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('t');

        await triggerKeyReleaseWaitObserved.WaitAsync(TestTimeout, CancellationToken.None);
        await _executor.DidNotReceive().ExpandAsync(Arg.Any<TextExpansionEntry>(), Arg.Any<CancellationToken>());

        triggerKeyPressed = false;

        await expansionStarted.WaitAsync(TestTimeout, CancellationToken.None);
        await _executor.Received(1).ExpandAsync(expansion, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ModifierReleasePollInterval_UsesDirectTypingInterval()
    {
        Assert.Equal(
            TextExpansionExecutionTimings.DirectTypingInterElementDelay,
            TextExpansionExecutionTimings.ModifierReleasePollInterval);
    }

    [Fact]
    public async Task Expansion_WhenModifierIsStillPressed_WaitsForReleaseBeforeExecuting()
    {
        _service.Start();

        var expansion = new TextExpansionEntry { Trigger = ":test", Replacement = "done" };
        _ = _storageService.GetCurrent().Returns(new List<TextExpansionEntry> { expansion });
        _ = _bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansionEntry>>(), out Arg.Any<TextExpansionEntry?>())
            .Returns(callInfo =>
            {
                callInfo[1] = expansion;
                return true;
            });

        var expansionStarted = new AsyncSignal();
        _ = _executor.ExpandAsync(Arg.Any<TextExpansionEntry>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                expansionStarted.Signal();
                return Task.CompletedTask;
            });
        var modifierReleaseWaitObserved = new AsyncSignal();
        var modifierPressed = true;
        _ = _inputProcessor.AreModifiersPressed.Returns(_ =>
        {
            modifierReleaseWaitObserved.Signal();
            return modifierPressed;
        });

        _inputCapture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
            this,
            new CapturedInputEventArgs { Type = InputEventType.Key, Code = 20, Value = 1 });
        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('t');

        await modifierReleaseWaitObserved.WaitAsync(TestTimeout, CancellationToken.None);
        await _executor.DidNotReceive().ExpandAsync(Arg.Any<TextExpansionEntry>(), Arg.Any<CancellationToken>());

        modifierPressed = false;

        await expansionStarted.WaitAsync(TestTimeout, CancellationToken.None);
        await _executor.Received(1).ExpandAsync(expansion, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_WhenCaptureStartFaultsAsynchronously_StopsService()
    {
        var startTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupObserved = new AsyncSignal();
        _ = _inputCapture.StartAsync(Arg.Any<CancellationToken>()).Returns(startTcs.Task);
        _inputCapture.When(x => x.Dispose()).Do(_ => cleanupObserved.Signal());

        _service.Start();
        Assert.True(_service.IsRunning);

        startTcs.SetException(new InvalidOperationException("startup failed"));

        await cleanupObserved.WaitAsync(TestTimeout, CancellationToken.None);

        Assert.False(_service.IsRunning);
        _inputCapture.Received(1).StopCapture();
        _inputCapture.Received(1).Dispose();

        Received.InOrder(() =>
        {
            _inputCapture.StopCapture();
            _inputCapture.Dispose();
        });
    }

    [Fact]
    public void Start_WhenCaptureStartFaultsSynchronously_CleansUpFailedCapture()
    {
        _ = _inputCapture.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("startup failed")));

        _service.Start();

        Assert.False(_service.IsRunning);
        _inputCapture.Received(1).StopCapture();
        _inputCapture.Received(1).Dispose();
    }

    [Fact]
    public async Task CaptureRecovery_AfterStopAndNewStart_DoesNotRestartTheNewSession()
    {
        var clock = new FakeTimeProvider();
        var captures = Enumerable.Range(0, 3).Select(_ => Substitute.For<IInputCapture>()).ToArray();
        var factoryCalls = 0;
        using var service = new TextExpansionService(
            _settingsService, _storageService, () => captures[factoryCalls++],
            _inputProcessor, _bufferState, _executor, clock);

        service.Start();
        captures[0].CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(
            captures[0], new InputCaptureErrorEventArgs("Connection lost"));
        var oldRecovery = Assert.IsAssignableFrom<Task>(service.RestartTask);

        service.StopExpansion();
        service.Start();
        clock.Advance(TextExpansionExecutionTimings.CaptureRecoveryDelay);
        await oldRecovery.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        Assert.Equal(2, factoryCalls);
        Assert.True(service.IsRunning);
        captures[1].DidNotReceive().StopCapture();
        captures[1].DidNotReceive().Dispose();
        _inputProcessor.Received(2).Reset();
    }

    [Fact]
    public async Task CaptureRecovery_NewSessionError_IsNotBlockedByOldRecoveryCompletion()
    {
        var clock = new FakeTimeProvider();
        var captures = Enumerable.Range(0, 3).Select(_ => Substitute.For<IInputCapture>()).ToArray();
        var factoryCalls = 0;
        using var service = new TextExpansionService(
            _settingsService, _storageService, () => captures[factoryCalls++],
            _inputProcessor, _bufferState, _executor, clock);

        service.Start();
        captures[0].CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(
            captures[0], new InputCaptureErrorEventArgs("Old connection lost"));
        var oldRecovery = Assert.IsAssignableFrom<Task>(service.RestartTask);
        service.StopExpansion();
        service.Start();
        captures[1].CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(
            captures[1], new InputCaptureErrorEventArgs("New connection lost"));
        var newRecovery = Assert.IsAssignableFrom<Task>(service.RestartTask);
        Assert.NotSame(oldRecovery, newRecovery);

        await oldRecovery.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);
        captures[1].CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(
            captures[1], new InputCaptureErrorEventArgs("Repeated new error"));
        Assert.Same(newRecovery, service.RestartTask);
        clock.Advance(TextExpansionExecutionTimings.CaptureRecoveryDelay);
        await newRecovery.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        Assert.Equal(3, factoryCalls);
        captures[1].Received(1).StopCapture();
        captures[1].Received(1).Dispose();
        Assert.True(service.IsRunning);
    }

    [Fact]
    public async Task StopExpansionAsync_CancelsAndSettlesDelayedRecoveryWithoutClockAdvance()
    {
        var clock = new FakeTimeProvider();
        using var service = new TextExpansionService(
            _settingsService, _storageService, () => _inputCapture,
            _inputProcessor, _bufferState, _executor, clock);
        service.Start();
        _inputCapture.CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(
            _inputCapture, new InputCaptureErrorEventArgs("Connection lost"));
        var recovery = Assert.IsAssignableFrom<Task>(service.RestartTask);

        await service.StopExpansionAsync(CancellationToken.None).WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        Assert.True(recovery.IsCompletedSuccessfully);
        Assert.False(service.IsRunning);
        _inputCapture.Received(1).Dispose();
    }

    [Fact]
    public async Task OnInputCaptureError_AfterStartup_RestartsCapture()
    {
        var firstCapture = Substitute.For<IInputCapture>();
        _ = firstCapture.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var secondCapture = Substitute.For<IInputCapture>();
        var secondStarted = new AsyncSignal();
        _ = secondCapture.StartAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            secondStarted.Signal();
            return Task.CompletedTask;
        });

        var factoryCallCount = 0;
        var service = new TextExpansionService(
            _settingsService,
            _storageService,
            () =>
            {
                factoryCallCount++;
                return factoryCallCount is 1 ? firstCapture : secondCapture;
            },
            _inputProcessor,
            _bufferState,
            _executor);

        service.Start();
        Assert.True(service.IsRunning);

        firstCapture.CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(firstCapture, new InputCaptureErrorEventArgs("Connection lost: daemon went away"));

        await secondStarted.WaitAsync(TestTimeout, CancellationToken.None);

        Assert.True(service.IsRunning);
        Assert.Equal(2, factoryCallCount);
        firstCapture.Received(1).StopCapture();
        firstCapture.Received(1).Dispose();
        _inputProcessor.Received(2).Reset();

        service.Dispose();
    }

    [Fact]
    public async Task OnInputCaptureError_WhenRestartFails_StopsService()
    {
        var firstCapture = Substitute.For<IInputCapture>();
        _ = firstCapture.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var cleanupObserved = new AsyncSignal();
        firstCapture.When(x => x.Dispose()).Do(_ => cleanupObserved.Signal());

        var factoryCallCount = 0;
        var service = new TextExpansionService(
            _settingsService,
            _storageService,
            () =>
            {
                factoryCallCount++;
                return factoryCallCount > 1
                    ? throw new InvalidOperationException("capture backend gone")
                    : firstCapture;
            },
            _inputProcessor,
            _bufferState,
            _executor);

        service.Start();
        Assert.True(service.IsRunning);

        firstCapture.CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(firstCapture, new InputCaptureErrorEventArgs("runtime failed"));
        var restartTask = service.RestartTask;
        Assert.NotNull(restartTask);

        await cleanupObserved.WaitAsync(TestTimeout, CancellationToken.None);
        await restartTask.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        Assert.False(service.IsRunning);
        Assert.Equal(2, factoryCallCount);

        service.Dispose();
    }
}
