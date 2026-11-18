using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

public sealed class TextExpansionExecutorClipboardTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ExpandAsync_WhenClipboardHeldNoText_ClearsReplacementFromClipboard(string? backup)
    {
        var clipboardService = Substitute.For<IClipboardService>();
        _ = clipboardService.IsSupported.Returns(returnThis: true);
        _ = clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(backup),
            Task.FromResult<string?>("replacement"), // Verification
            Task.FromResult<string?>("replacement")); // Restore guard check
        _ = clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await using var executor = new TextExpansionExecutor(
            clipboardService,
            Substitute.For<IKeyboardLayoutService>(),
            () => new TestInputSimulator(),
            new PacedTimeProvider());

        await executor.ExpandAsync(new TextExpansionEntry(":a", "replacement"), TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _ = clipboardService.SetTextAsync("replacement", Arg.Any<CancellationToken>());
            _ = clipboardService.SetTextAsync(string.Empty, Arg.Any<CancellationToken>());
        });
        await clipboardService.Received(2).SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardBackupReadFails_TypesDirectlyWithoutTouchingClipboard()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        _ = clipboardService.IsSupported.Returns(returnThis: true);
        _ = clipboardService.GetTextAsync(Arg.Any<CancellationToken>())
            .Returns<Task<string?>>(_ => throw new InvalidOperationException("clipboard unavailable"));

        var inputSimulator = new UnicodeCapableTestInputSimulator();
        await using var executor = new TextExpansionExecutor(
            clipboardService,
            Substitute.For<IKeyboardLayoutService>(),
            () => inputSimulator,
            new PacedTimeProvider());

        await executor.ExpandAsync(new TextExpansionEntry(":a", "replacement"), TestContext.Current.CancellationToken);

        await clipboardService.DidNotReceive().SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Equal("replacement", string.Concat(inputSimulator.TypedText));
        Assert.DoesNotContain(InputEventCode.KEY_V, inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenPasteModeSucceeds_PreparesClipboardBeforeDeletingTriggerAndRestoresAfterPaste()
    {
        var clipboardService = new RecordingClipboardService(
            backupValue: "old-value",
            verificationValue: "replacement",
            restoreGuardValue: "replacement");
        var inputSimulator = new RecordingInputSimulator(clipboardService.Events);
        await using var executor = new TextExpansionExecutor(
            clipboardService,
            Substitute.For<IKeyboardLayoutService>(),
            () => inputSimulator,
            new PacedTimeProvider());

        await executor.ExpandAsync(new TextExpansionEntry(":a", "replacement"), TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                "clipboard:get:1",
                "clipboard:set:replacement",
                "clipboard:get:2",
                Key(InputEventCode.KEY_BACKSPACE),
                Key(InputEventCode.KEY_BACKSPACE),
                Key(InputEventCode.KEY_LEFTCTRL),
                Key(InputEventCode.KEY_V),
                "clipboard:get:3",
                "clipboard:set:old-value",
            ],
            clipboardService.Events);
    }

    [Fact]
    public async Task ExpandAsync_WhenStandardPasteUsesMetaModifier_UsesMetaPasteShortcut()
    {
        var clipboardService = new RecordingClipboardService(
            backupValue: "old-value",
            verificationValue: "replacement",
            restoreGuardValue: "replacement");
        var inputSimulator = new MetaPasteRecordingInputSimulator(clipboardService.Events);
        await using var executor = new TextExpansionExecutor(
            clipboardService,
            Substitute.For<IKeyboardLayoutService>(),
            () => inputSimulator,
            new PacedTimeProvider());

        await executor.ExpandAsync(new TextExpansionEntry(":a", "replacement"), TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                "clipboard:get:1",
                "clipboard:set:replacement",
                "clipboard:get:2",
                Key(InputEventCode.KEY_BACKSPACE),
                Key(InputEventCode.KEY_BACKSPACE),
                Key(InputEventCode.KEY_LEFTMETA),
                Key(InputEventCode.KEY_V),
                "clipboard:get:3",
                "clipboard:set:old-value",
            ],
            clipboardService.Events);
    }

    [Theory]
    [InlineData("user-new-copy")]
    [InlineData(null)]
    public async Task ExpandAsync_WhenClipboardNoLongerHoldsReplacementAfterPaste_DoesNotRestoreOldClipboard(string? clipboardAfterPaste)
    {
        var clipboardService = Substitute.For<IClipboardService>();
        _ = clipboardService.IsSupported.Returns(returnThis: true);
        _ = clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<string?>("old-value"),
            Task.FromResult<string?>("replacement"),
            Task.FromResult(clipboardAfterPaste)); // Restore guard check
        _ = clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await using var executor = new TextExpansionExecutor(
            clipboardService,
            Substitute.For<IKeyboardLayoutService>(),
            () => new TestInputSimulator(),
            new PacedTimeProvider());

        await executor.ExpandAsync(new TextExpansionEntry(":a", "replacement"), TestContext.Current.CancellationToken);

        await clipboardService.Received(1).SetTextAsync("replacement", Arg.Any<CancellationToken>());
        await clipboardService.Received(1).SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardWriteTimesOut_CancelsWriteAndFallsBackToDirectTyping()
    {
        var time = new PacedTimeProvider();
        var clipboardService = new BlockingClipboardService();
        var inputSimulator = new UnicodeCapableTestInputSimulator();
        await using var executor = new TextExpansionExecutor(
            clipboardService,
            Substitute.For<IKeyboardLayoutService>(),
            () => inputSimulator,
            time);

        var expansion = executor.ExpandAsync(new TextExpansionEntry(":a", "replacement"), TestContext.Current.CancellationToken);
        await time.ElapseTimeoutAsync(2); // backup read, then the clipboard write
        await expansion.WaitAsync(TestTimeout, TimeProvider.System, TestContext.Current.CancellationToken);

        await clipboardService.CancellationObserved.WaitAsync(TestTimeout, TimeProvider.System, TestContext.Current.CancellationToken);
        Assert.Equal("replacement", string.Concat(inputSimulator.TypedText));
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardVerificationDoesNotMatch_SkipsPasteAndFallsBackToDirectTyping()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        _ = clipboardService.IsSupported.Returns(returnThis: true);
        _ = clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<string?>("old-value"),
            Task.FromResult<string?>("stale-host-clipboard"));
        _ = clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var inputSimulator = new UnicodeCapableTestInputSimulator();
        await using var executor = new TextExpansionExecutor(
            clipboardService,
            Substitute.For<IKeyboardLayoutService>(),
            () => inputSimulator,
            new PacedTimeProvider());

        await executor.ExpandAsync(new TextExpansionEntry(":a", "replacement"), TestContext.Current.CancellationToken);

        Assert.Equal("replacement", string.Concat(inputSimulator.TypedText));
        Assert.DoesNotContain(InputEventCode.KEY_V, inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardVerificationSucceedsOnRetry_PastesReplacement()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        _ = clipboardService.IsSupported.Returns(returnThis: true);
        _ = clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<string?>("old-value"),
            Task.FromResult<string?>("stale-host-clipboard"),
            Task.FromResult<string?>("replacement"),
            Task.FromResult<string?>("replacement"));
        _ = clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var inputSimulator = new UnicodeCapableTestInputSimulator();
        await using var executor = new TextExpansionExecutor(
            clipboardService,
            Substitute.For<IKeyboardLayoutService>(),
            () => inputSimulator,
            new PacedTimeProvider());

        await executor.ExpandAsync(new TextExpansionEntry(":a", "replacement"), TestContext.Current.CancellationToken);

        await clipboardService.Received(1).SetTextAsync("replacement", Arg.Any<CancellationToken>());
        Assert.Contains(InputEventCode.KEY_V, inputSimulator.PressedKeys);
        Assert.Empty(inputSimulator.TypedText);
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardVerificationFailsAfterWrite_RestoresOldClipboardBeforeDirectFallback()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        _ = clipboardService.IsSupported.Returns(returnThis: true);
        _ = clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<string?>("old-value"),
            Task.FromResult<string?>("stale-host-clipboard"),
            Task.FromResult<string?>("stale-host-clipboard"),
            Task.FromResult<string?>("replacement"));
        _ = clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var inputSimulator = new UnicodeCapableTestInputSimulator();
        await using var executor = new TextExpansionExecutor(
            clipboardService,
            Substitute.For<IKeyboardLayoutService>(),
            () => inputSimulator,
            new PacedTimeProvider());

        await executor.ExpandAsync(new TextExpansionEntry(":a", "replacement"), TestContext.Current.CancellationToken);

        await clipboardService.Received(1).SetTextAsync("replacement", Arg.Any<CancellationToken>());
        await clipboardService.Received(1).SetTextAsync("old-value", Arg.Any<CancellationToken>());
        Assert.Equal("replacement", string.Concat(inputSimulator.TypedText));
        Assert.DoesNotContain(InputEventCode.KEY_V, inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardVerificationFailsAndClipboardChanged_DoesNotRestoreOldClipboard()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        _ = clipboardService.IsSupported.Returns(returnThis: true);
        _ = clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<string?>("old-value"),
            Task.FromResult<string?>("stale-host-clipboard"),
            Task.FromResult<string?>("user-new-copy"));
        _ = clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var inputSimulator = new UnicodeCapableTestInputSimulator();
        await using var executor = new TextExpansionExecutor(
            clipboardService,
            Substitute.For<IKeyboardLayoutService>(),
            () => inputSimulator,
            new PacedTimeProvider());

        await executor.ExpandAsync(new TextExpansionEntry(":a", "replacement"), TestContext.Current.CancellationToken);

        await clipboardService.Received(1).SetTextAsync("replacement", Arg.Any<CancellationToken>());
        await clipboardService.DidNotReceive().SetTextAsync("old-value", Arg.Any<CancellationToken>());
        Assert.Equal("replacement", string.Concat(inputSimulator.TypedText));
        Assert.DoesNotContain(InputEventCode.KEY_V, inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardVerificationReadNeverCompletes_SkipsPasteAndFallsBackToDirectTyping()
    {
        var time = new PacedTimeProvider();
        var inputSimulator = new UnicodeCapableTestInputSimulator();
        await using var executor = new TextExpansionExecutor(
            new NonCompletingReadAfterWriteClipboardService(),
            Substitute.For<IKeyboardLayoutService>(),
            () => inputSimulator,
            time);

        var expansion = executor.ExpandAsync(new TextExpansionEntry(":a", "replacement"), TestContext.Current.CancellationToken);
        await time.ElapseTimeoutAsync(3); // backup read, clipboard write, then the verification read
        await time.ElapseTimeoutAsync(4); // the restore-guard read after the failed verification
        await expansion.WaitAsync(TestTimeout, TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.Equal("replacement", string.Concat(inputSimulator.TypedText));
        Assert.DoesNotContain(InputEventCode.KEY_V, inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardReadBlocksBeforeReturningTask_TimesOutAndFallsBackToDirectTyping()
    {
        var time = new PacedTimeProvider();
        var clipboardService = new SynchronouslyBlockingReadClipboardService();
        var inputSimulator = new UnicodeCapableTestInputSimulator();
        await using var executor = new TextExpansionExecutor(
            clipboardService,
            Substitute.For<IKeyboardLayoutService>(),
            () => inputSimulator,
            time);

        var expansion = executor.ExpandAsync(new TextExpansionEntry(":a", "replacement"), TestContext.Current.CancellationToken);
        await clipboardService.WaitForReadStartedAsync(TestTimeout);
        await time.ElapseTimeoutAsync(1); // the backup read
        await expansion.WaitAsync(TestTimeout, TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.Equal("replacement", string.Concat(inputSimulator.TypedText));
        Assert.DoesNotContain(InputEventCode.KEY_V, inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardGuaranteesImmediateReadback_SkipsVerification()
    {
        var clipboardService = new ImmediateReadbackClipboardService();
        var inputSimulator = new UnicodeCapableTestInputSimulator();
        await using var executor = new TextExpansionExecutor(
            clipboardService,
            Substitute.For<IKeyboardLayoutService>(),
            () => inputSimulator,
            new PacedTimeProvider());

        await executor.ExpandAsync(new TextExpansionEntry(":a", "replacement"), TestContext.Current.CancellationToken);

        Assert.Equal(2, clipboardService.ReadCount); // backup and restore guard, no verification read
        Assert.Contains(InputEventCode.KEY_V, inputSimulator.PressedKeys);
        Assert.Empty(inputSimulator.TypedText);
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardVerificationFailsAndDirectFallbackUnsupported_DoesNotEraseTrigger()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        _ = clipboardService.IsSupported.Returns(returnThis: true);
        _ = clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<string?>("old-value"),
            Task.FromResult<string?>("stale-host-clipboard"));
        _ = clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        _ = keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns(default((int KeyCode, bool Shift, bool AltGr)?));
        var inputSimulator = new TestInputSimulator();
        await using var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator,
            new PacedTimeProvider());

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExpandAsync(new TextExpansionEntry(":emoji", "🙂"), TestContext.Current.CancellationToken));

        Assert.Empty(inputSimulator.PressedKeys);
    }

    private static string Key(int keyCode) => string.Create(CultureInfo.InvariantCulture, $"input:key:{keyCode}");
}
