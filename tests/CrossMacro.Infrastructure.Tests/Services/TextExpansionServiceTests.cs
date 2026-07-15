
namespace CrossMacro.Infrastructure.Tests.Services;

public class TextExpansionServiceTests
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
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(new AppSettings { EnableTextExpansion = true });

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

    [Fact]
    public async Task Start_WhenEnabled_StartsInputCaptureAndResetsState()
    {
        // Act
        _service.Start();

        // Assert
        Assert.True(_service.IsRunning);
        _storageService.Received(1).Load();
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

        _storageService.Received(1).Load();
        _inputCapture.Received(1).Configure(captureMouse: false, captureKeyboard: true);
        await _inputCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
        _inputProcessor.Received(1).Reset();
        _bufferState.Received(1).Clear();
    }

    [Fact]
    public async Task Start_WhenDisabled_DoesNotStart()
    {
        // Arrange
        _settingsService.Current.Returns(new AppSettings { EnableTextExpansion = false });

        // Act
        _service.Start();

        // Assert
        Assert.False(_service.IsRunning);
        _storageService.DidNotReceive().Load();
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
    public async Task Start_AfterDispose_DoesNotRestartCapture()
    {
        _service.Start();
        _service.Dispose();

        _service.Start();

        Assert.False(_service.IsRunning);
        _storageService.Received(1).Load();
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
        _storageService.GetCurrent().Returns(new List<TextExpansionEntry> { expansion });
        _bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansionEntry>>(), out Arg.Any<TextExpansionEntry?>())
            .Returns(callInfo =>
            {
                callInfo[1] = expansion;
                return true;
            });

        var invocationCount = 0;
        var firstExpansionStarted = new AsyncSignal();
        var secondExpansionStarted = new AsyncSignal();
        _executor.ExpandAsync(Arg.Any<TextExpansionEntry>())
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
        await firstExpansionStarted.WaitAsync(TestTimeout);

        for (var attempt = 0; attempt < 100 && Volatile.Read(ref invocationCount) < 2; attempt++)
        {
            _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('a');
            await Task.Delay(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        await secondExpansionStarted.WaitAsync(TestTimeout);
        await _executor.Received(2).ExpandAsync(Arg.Any<TextExpansionEntry>());
        Assert.True(_service.IsRunning);
    }

    [Fact]
    public async Task Expansion_WhenAlreadyRunning_DropsTriggersWithoutQueueingAndRecoversForLaterTrigger()
    {
        _service.Start();

        var expansion = new TextExpansionEntry { Trigger = ":a", Replacement = "alpha" };
        _storageService.GetCurrent().Returns(new List<TextExpansionEntry> { expansion });
        _bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansionEntry>>(), out Arg.Any<TextExpansionEntry?>())
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
        _executor.ExpandAsync(Arg.Any<TextExpansionEntry>())
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
        await firstStarted.WaitAsync(TestTimeout);

        for (var index = 0; index < 32; index++)
        {
            _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('a');
        }

        Assert.Equal(1, Volatile.Read(ref invocationCount));
        await _executor.Received(1).ExpandAsync(expansion);

        releaseFirst.SetResult();
        await firstFinished.WaitAsync(TestTimeout);

        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('a');
        await secondStarted.WaitAsync(TestTimeout);

        Assert.Equal(2, Volatile.Read(ref invocationCount));
        await _executor.Received(2).ExpandAsync(expansion);
    }

    [Fact]
    public async Task Expansion_WhenTriggerLastKeyIsStillPressed_WaitsForReleaseBeforeExecuting()
    {
        _service.Start();

        var expansion = new TextExpansionEntry { Trigger = ":test", Replacement = "done" };
        _storageService.GetCurrent().Returns(new List<TextExpansionEntry> { expansion });
        _bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansionEntry>>(), out Arg.Any<TextExpansionEntry?>())
            .Returns(callInfo =>
            {
                callInfo[1] = expansion;
                return true;
            });

        var expansionStarted = new AsyncSignal();
        _executor.ExpandAsync(Arg.Any<TextExpansionEntry>())
            .Returns(_ =>
            {
                expansionStarted.Signal();
                return Task.CompletedTask;
            });
        var triggerKeyReleaseWaitObserved = new AsyncSignal();
        var triggerKeyPressed = true;
        _inputProcessor.IsKeyPressed(20).Returns(_ =>
        {
            triggerKeyReleaseWaitObserved.Signal();
            return triggerKeyPressed;
        });

        _inputCapture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
            this,
            new CapturedInputEventArgs { Type = InputEventType.Key, Code = 20, Value = 1 });
        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('t');

        await triggerKeyReleaseWaitObserved.WaitAsync(TestTimeout);
        await _executor.DidNotReceive().ExpandAsync(Arg.Any<TextExpansionEntry>());

        triggerKeyPressed = false;

        await expansionStarted.WaitAsync(TestTimeout);
        await _executor.Received(1).ExpandAsync(expansion);
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
        _storageService.GetCurrent().Returns(new List<TextExpansionEntry> { expansion });
        _bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansionEntry>>(), out Arg.Any<TextExpansionEntry?>())
            .Returns(callInfo =>
            {
                callInfo[1] = expansion;
                return true;
            });

        var expansionStarted = new AsyncSignal();
        _executor.ExpandAsync(Arg.Any<TextExpansionEntry>())
            .Returns(_ =>
            {
                expansionStarted.Signal();
                return Task.CompletedTask;
            });
        var modifierReleaseWaitObserved = new AsyncSignal();
        var modifierPressed = true;
        _inputProcessor.AreModifiersPressed.Returns(_ =>
        {
            modifierReleaseWaitObserved.Signal();
            return modifierPressed;
        });

        _inputCapture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
            this,
            new CapturedInputEventArgs { Type = InputEventType.Key, Code = 20, Value = 1 });
        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('t');

        await modifierReleaseWaitObserved.WaitAsync(TestTimeout);
        await _executor.DidNotReceive().ExpandAsync(Arg.Any<TextExpansionEntry>());

        modifierPressed = false;

        await expansionStarted.WaitAsync(TestTimeout);
        await _executor.Received(1).ExpandAsync(expansion);
    }

    [Fact]
    public async Task Start_WhenCaptureStartFaultsAsynchronously_StopsService()
    {
        var startTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupObserved = new AsyncSignal();
        _inputCapture.StartAsync(Arg.Any<CancellationToken>()).Returns(startTcs.Task);
        _inputCapture.When(x => x.Dispose()).Do(_ => cleanupObserved.Signal());

        _service.Start();
        Assert.True(_service.IsRunning);

        startTcs.SetException(new InvalidOperationException("startup failed"));

        await cleanupObserved.WaitAsync(TestTimeout);

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
        _inputCapture.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("startup failed")));

        _service.Start();

        Assert.False(_service.IsRunning);
        _inputCapture.Received(1).StopCapture();
        _inputCapture.Received(1).Dispose();
    }

    [Fact]
    public async Task OnInputCaptureError_AfterStartup_StopsServiceWithoutRestart()
    {
        var firstCapture = Substitute.For<IInputCapture>();
        var cleanupObserved = new AsyncSignal();
        firstCapture.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        firstCapture.When(x => x.Dispose()).Do(_ => cleanupObserved.Signal());

        var factoryCallCount = 0;
        var service = new TextExpansionService(
            _settingsService,
            _storageService,
            () =>
            {
                factoryCallCount++;
                return firstCapture;
            },
            _inputProcessor,
            _bufferState,
            _executor);

        service.Start();
        Assert.True(service.IsRunning);

        firstCapture.CaptureError += Raise.Event<EventHandler<InputCaptureErrorEventArgs>>(firstCapture, new InputCaptureErrorEventArgs("runtime failed"));

        await cleanupObserved.WaitAsync(TestTimeout);

        Assert.False(service.IsRunning);
        Assert.Equal(1, factoryCallCount);
        firstCapture.Received(1).StopCapture();
        firstCapture.Received(1).Dispose();

        service.Dispose();
    }
}
