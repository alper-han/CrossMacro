using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.TextExpansion;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.TextExpansion;
using CrossMacro.TestInfrastructure;
using NSubstitute;
using Xunit;

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
        _inputCapture.Received(1).Configure(false, true);
        await _inputCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
        
        _inputProcessor.Received(1).Reset();
        _bufferState.Received(1).Clear();
    }

    [Fact]
    public async Task Start_WhenCalledTwice_DoesNotCreateOrStartSecondCapture()
    {
        _service.Start();

        _service.Start();

        _inputCapture.Received(1).Configure(false, true);
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
        await _inputCapture.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Stop_StopsInputCapture()
    {
        // Arrange
        _service.Start();

        // Act
        _service.Stop();

        // Assert
        _inputCapture.Received(1).Stop();
        _inputCapture.Received(1).Dispose();
    }

    [Fact]
    public void Stop_WhenCalledTwice_IsIdempotent()
    {
        _service.Start();

        _service.Stop();
        _service.Stop();

        _inputCapture.Received(1).Stop();
        _inputCapture.Received(1).Dispose();
        Assert.False(_service.IsRunning);
    }
    
    [Fact]
    public void OnInputReceived_DelegatesToProcessor()
    {
        // Arrange
        _service.Start();
        var eventArgs = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 30, Value = 1 };

        // Act
        _inputCapture.InputReceived += Raise.Event<EventHandler<InputCaptureEventArgs>>(this, eventArgs);

        // Assert
        _inputProcessor.Received(1).ProcessEvent(eventArgs);
    }

    [Fact]
    public async Task Expansion_WhenExecutorThrows_ExceptionIsHandledAndSubsequentExpansionStillRuns()
    {
        // Arrange
        _service.Start();

        var expansion = new TextExpansion { Trigger = ":a", Replacement = "alpha" };
        _storageService.GetCurrent().Returns(new List<TextExpansion> { expansion });
        _bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansion>>(), out Arg.Any<TextExpansion?>())
            .Returns(callInfo =>
            {
                callInfo[1] = expansion;
                return true;
            });

        var invocationCount = 0;
        var secondExpansionStarted = new AsyncSignal();
        _executor.ExpandAsync(Arg.Any<TextExpansion>())
            .Returns(_ =>
            {
                invocationCount++;

                if (invocationCount == 2)
                {
                    secondExpansionStarted.Signal();
                }

                return invocationCount == 1
                    ? Task.FromException(new InvalidOperationException("boom"))
                    : Task.CompletedTask;
            });

        // Act
        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('a');
        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('a');

        // Assert
        await secondExpansionStarted.WaitAsync(TestTimeout);
        await _executor.Received(2).ExpandAsync(Arg.Any<TextExpansion>());
        Assert.True(_service.IsRunning);
    }

    [Fact]
    public async Task Expansion_WhenTriggerLastKeyIsStillPressed_WaitsForReleaseBeforeExecuting()
    {
        _service.Start();

        var expansion = new TextExpansion { Trigger = ":test", Replacement = "done" };
        _storageService.GetCurrent().Returns(new List<TextExpansion> { expansion });
        _bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansion>>(), out Arg.Any<TextExpansion?>())
            .Returns(callInfo =>
            {
                callInfo[1] = expansion;
                return true;
            });

        var expansionStarted = new AsyncSignal();
        _executor.ExpandAsync(Arg.Any<TextExpansion>())
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

        _inputCapture.InputReceived += Raise.Event<EventHandler<InputCaptureEventArgs>>(
            this,
            new InputCaptureEventArgs { Type = InputEventType.Key, Code = 20, Value = 1 });
        _inputProcessor.CharacterReceived += Raise.Event<Action<char>>('t');

        await triggerKeyReleaseWaitObserved.WaitAsync(TestTimeout);
        await _executor.DidNotReceive().ExpandAsync(Arg.Any<TextExpansion>());

        triggerKeyPressed = false;

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
        _inputCapture.Received(1).Stop();
        _inputCapture.Received(1).Dispose();

        Received.InOrder(() =>
        {
            _inputCapture.Stop();
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
        _inputCapture.Received(1).Stop();
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

        firstCapture.Error += Raise.Event<EventHandler<string>>(firstCapture, "runtime failed");

        await cleanupObserved.WaitAsync(TestTimeout);

        Assert.False(service.IsRunning);
        Assert.Equal(1, factoryCallCount);
        firstCapture.Received(1).Stop();
        firstCapture.Received(1).Dispose();

        service.Dispose();
    }
}
