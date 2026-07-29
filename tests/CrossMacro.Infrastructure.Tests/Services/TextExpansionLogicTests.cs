
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class TextExpansionLogicTests : IDisposable
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    private readonly ISettingsService _settingsService;
    private readonly ITextExpansionStorageService _storageService;
    private readonly IKeyboardLayoutService _layoutService;
    private readonly IInputCapture _inputCapture;

    // Components (Real or Mocked as needed)
    private readonly InputProcessor _inputProcessor;
    private readonly TextBufferState _bufferState;
    private readonly ITextExpansionExecutor _executor;

    private readonly TextExpansionService _service;

    public TextExpansionLogicTests()
    {
        _settingsService = Substitute.For<ISettingsService>();
        _ = _settingsService.Current.Returns(new AppSettings { EnableTextExpansion = true });

        _storageService = Substitute.For<ITextExpansionStorageService>();
        _layoutService = Substitute.For<IKeyboardLayoutService>();
        _inputCapture = Substitute.For<IInputCapture>();

        // Use Real Logic Components to test the flow
        _inputProcessor = new InputProcessor(_layoutService);
        _bufferState = new TextBufferState();
        _executor = Substitute.For<ITextExpansionExecutor>();

        _service = new TextExpansionService(
            _settingsService,
            _storageService,
            () => _inputCapture,
            _inputProcessor,
            _bufferState,
            _executor);

        // Default mock for typing
        _ = _layoutService.GetCharFromKeyCode(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns((char?)null); // Default null unless specified

        _service.Start();
    }

    public void Dispose() => _service.Dispose();

    [Fact]
    public async Task ExpansionTriggered_WhenBufferMatches()
    {
        // Arrange
        var expansion = new Core.Models.TextExpansionEntry("abc", "expanded");
        _ = _storageService.GetCurrent().Returns(new List<Core.Models.TextExpansionEntry> { expansion });
        var expansionTriggered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _executor
            .ExpandAsync(expansion, Arg.Any<CancellationToken>())
            .Returns(unusedCallInfo =>
            {
                _ = expansionTriggered.TrySetResult(true);
                return Task.CompletedTask;
            });

        SetupKey(30, 'a');
        SetupKey(48, 'b');
        SetupKey(46, 'c');

        // Act
        RaiseKey(30);
        RaiseKey(48);
        RaiseKey(46);
        _ = await expansionTriggered.Task.WaitAsync(TestTimeout);

        // Assert
        await _executor.Received(1).ExpandAsync(expansion, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Buffer_Clears_AfterMatch()
    {
        // Arrange
        var expansion = new Core.Models.TextExpansionEntry("abc", "expanded");
        _ = _storageService.GetCurrent().Returns(new List<Core.Models.TextExpansionEntry> { expansion });
        var expansionCount = 0;
        var firstExpansionStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstExpansionAllowedToFinish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondExpansionTriggered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = _executor
            .ExpandAsync(expansion, Arg.Any<CancellationToken>())
            .Returns(async unusedCallInfo =>
            {
                var currentCount = Interlocked.Increment(ref expansionCount);
                if (currentCount is 1)
                {
                    _ = firstExpansionStarted.TrySetResult(true);
                    _ = await firstExpansionAllowedToFinish.Task;
                }
                else if (currentCount is 2)
                {
                    _ = secondExpansionTriggered.TrySetResult(true);
                }
            });

        SetupKey(30, 'a');
        SetupKey(48, 'b');
        SetupKey(46, 'c');
        SetupKey(32, 'd');

        // Act - Trigger once, then continue typing and trigger again.
        RaiseKey(30);
        RaiseKey(48);
        RaiseKey(46);

        // Wait for first expansion to start
        _ = await firstExpansionStarted.Task.WaitAsync(TestTimeout);

        // Allow it to finish and yield to let background thread execute the finally block (Resume capture)
        _ = firstExpansionAllowedToFinish.TrySetResult(true);
        await Task.Delay(50);

        RaiseKey(32);
        RaiseKey(30);
        RaiseKey(48);
        RaiseKey(46);
        _ = await secondExpansionTriggered.Task.WaitAsync(TestTimeout);

        // Assert - Should trigger again
        await _executor.Received(2).ExpandAsync(expansion, Arg.Any<CancellationToken>());
    }

    private void SetupKey(int code, char c)
    {
        _ = _layoutService.GetCharFromKeyCode(code, Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(c);
    }

    private void RaiseKey(int code)
    {
        _inputCapture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
            this,
            new CapturedInputEventArgs { Type = InputEventType.Key, Code = code, Value = 1 }); // Press

        _inputCapture.InputReceived += Raise.Event<EventHandler<CapturedInputEventArgs>>(
            this,
            new CapturedInputEventArgs { Type = InputEventType.Key, Code = code, Value = 0 }); // Release
    }
}
