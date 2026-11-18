namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

[Collection(GlobalCoreLoggerGroup.Name)]
public sealed class TextExpansionLogPrivacyTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task TriggerDetection_DoesNotLogTriggerOrReplacementText()
    {
        var trigger = $"trigger-{Guid.NewGuid():N}";
        var replacement = $"replacement-{Guid.NewGuid():N}";

        var settingsService = SettingsServiceSubstitute.Create();
        _ = settingsService.Current.Returns(new AppSettings { EnableTextExpansion = true });

        var storageService = Substitute.For<ITextExpansionStorageService>();
        var inputCapture = Substitute.For<IInputCapture>();
        _ = inputCapture.ProviderName.Returns("test");
        var inputProcessor = Substitute.For<IInputProcessor>();
        var bufferState = Substitute.For<ITextBufferState>();
        var executor = Substitute.For<ITextExpansionExecutor>();

        var match = new TextExpansionEntry(trigger, replacement);
        _ = storageService.GetCurrent().Returns(new List<TextExpansionEntry> { match });
        _ = bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansionEntry>>(), out Arg.Any<TextExpansionEntry?>())
            .Returns(callInfo =>
            {
                callInfo[1] = match;
                return true;
            });

        var executorCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = executor.ExpandAsync(match, Arg.Any<CancellationToken>()).Returns(unusedCallInfo =>
        {
            _ = executorCalled.TrySetResult(true);
            return Task.CompletedTask;
        });

        var logger = new RecordingCoreLogger();
        using (CoreLogging.Log.PushLogger(logger))
        {
            using var service = new TextExpansionService(
                settingsService,
                storageService,
                () => inputCapture,
                inputProcessor,
                bufferState,
                executor);

            service.Start();
            inputProcessor.CharacterReceived += Raise.Event<Action<char>>('x');

            _ = await executorCalled.Task.WaitAsync(TestTimeout, TimeProvider.System, TestContext.Current.CancellationToken);
        }

        Assert.NotEmpty(logger.Entries);
        Assert.DoesNotContain(logger.Entries, entry => entry.Mentions(trigger));
        Assert.DoesNotContain(logger.Entries, entry => entry.Mentions(replacement));
    }

    [Fact]
    public async Task FallbackTyping_DoesNotLogReplacementText()
    {
        var replacement = $"secret-{Guid.NewGuid():N}";
        var clipboardService = Substitute.For<IClipboardService>();
        _ = clipboardService.IsSupported.Returns(returnThis: false);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        _ = keyboardLayoutService.GetInputForChar(Arg.Any<char>()).Returns((30, false, false));

        await using var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => new TestInputSimulator(),
            new PacedTimeProvider());

        var logger = new RecordingCoreLogger();
        using (CoreLogging.Log.PushLogger(logger))
        {
            await executor.ExpandAsync(new TextExpansionEntry(":a", replacement), TestContext.Current.CancellationToken);
        }

        Assert.NotEmpty(logger.Entries);
        Assert.DoesNotContain(logger.Entries, entry => entry.Mentions(replacement));
    }

    [Fact]
    public async Task ClipboardPaste_DoesNotLogReplacementOrPreviousClipboardText()
    {
        var replacement = $"secret-{Guid.NewGuid():N}";
        var previousClipboard = $"private-{Guid.NewGuid():N}";
        var clipboardService = new RecordingClipboardService(
            backupValue: previousClipboard,
            verificationValue: "stale-host-clipboard",
            restoreGuardValue: "stale-host-clipboard");

        await using var executor = new TextExpansionExecutor(
            clipboardService,
            Substitute.For<IKeyboardLayoutService>(),
            () => new UnicodeCapableTestInputSimulator(),
            new PacedTimeProvider());

        var logger = new RecordingCoreLogger();
        using (CoreLogging.Log.PushLogger(logger))
        {
            await executor.ExpandAsync(new TextExpansionEntry(":a", replacement), TestContext.Current.CancellationToken);
        }

        // The failed verification takes the warning path that must not echo clipboard contents.
        Assert.Contains(logger.Entries, entry => entry.Level is CoreLogging.CoreLogLevel.Warning);
        Assert.DoesNotContain(logger.Entries, entry => entry.Mentions(replacement));
        Assert.DoesNotContain(logger.Entries, entry => entry.Mentions(previousClipboard));
    }
}
