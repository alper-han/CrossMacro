namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

public sealed class TextExpansionExecutorDirectTypingTests
{
    private const int KeyA = 30;
    private const int KeyShift = 42;
    private const int KeyB = 48;

    [Fact]
    public async Task ExpandAsync_WhenDirectTypingMode_DoesNotTouchClipboard()
    {
        var clipboardService = CreateClipboard(isSupported: true);
        var inputSimulator = new TestInputSimulator();
        await using var executor = CreateExecutor(clipboardService, CreateLayout(static _ => (KeyA, false, false)), inputSimulator);

        var expansion = new TextExpansionEntry(
            ":a",
            "typed",
            method: PasteMethod.CtrlShiftV,
            insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion, TestContext.Current.CancellationToken);

        _ = await clipboardService.DidNotReceive().GetTextAsync(Arg.Any<CancellationToken>());
        await clipboardService.DidNotReceive().SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Contains(KeyA, inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenDirectTypingNeedsUnicode_UsesSimulatorUnicodeCapability()
    {
        var clipboardService = CreateClipboard(isSupported: true);
        var inputSimulator = new UnicodeCapableTestInputSimulator();
        await using var executor = CreateExecutor(clipboardService, CreateLayout(static _ => null), inputSimulator);

        var expansion = new TextExpansionEntry(":emoji", "🙂", insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion, TestContext.Current.CancellationToken);

        _ = await clipboardService.DidNotReceive().GetTextAsync(Arg.Any<CancellationToken>());
        await clipboardService.DidNotReceive().SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Contains("🙂", inputSimulator.TypedText, StringComparer.Ordinal);
    }

    [Fact]
    public async Task ExpandAsync_WhenDirectTypingAndSimulatorSupportsUnicode_PrefersUnicodeOverKeyboardLayout()
    {
        var keyboardLayoutService = CreateLayout(static _ => (KeyA, false, false));
        var inputSimulator = new UnicodeCapableTestInputSimulator();
        await using var executor = CreateExecutor(CreateClipboard(isSupported: true), keyboardLayoutService, inputSimulator);

        var expansion = new TextExpansionEntry(":ascii", "typed", insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion, TestContext.Current.CancellationToken);

        Assert.Equal("typed", string.Concat(inputSimulator.TypedText));
        Assert.DoesNotContain(KeyA, inputSimulator.PressedKeys);
        _ = keyboardLayoutService.DidNotReceiveWithAnyArgs().GetInputForChar(default);
    }

    [Fact]
    public async Task ExpandAsync_WhenDirectTypingAndSimulatorSupportsTaggedUnicode_PropagatesTextExpansionMarker()
    {
        var inputSimulator = new TaggedUnicodeCapableTestInputSimulator();
        await using var executor = CreateExecutor(CreateClipboard(isSupported: true), CreateLayout(static _ => null), inputSimulator);

        var expansion = new TextExpansionEntry(":emoji", "🙂", insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion, TestContext.Current.CancellationToken);

        Assert.Equal(
            [("🙂", InputEventMarkers.TextExpansionKeyboardEvent)],
            inputSimulator.TaggedText);
        Assert.Empty(inputSimulator.TypedText);
    }

    [Fact]
    public async Task ExpandAsync_WhenPasteFallsBackToDirectTyping_UsesSimulatorUnicodeCapability()
    {
        var clipboardService = CreateClipboard(isSupported: false);
        var inputSimulator = new UnicodeCapableTestInputSimulator();
        await using var executor = CreateExecutor(clipboardService, CreateLayout(static _ => null), inputSimulator);

        await executor.ExpandAsync(new TextExpansionEntry(":emoji", "🙂"), TestContext.Current.CancellationToken);

        _ = await clipboardService.DidNotReceive().GetTextAsync(Arg.Any<CancellationToken>());
        await clipboardService.DidNotReceive().SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Contains("🙂", inputSimulator.TypedText, StringComparer.Ordinal);
    }

    [LinuxFact]
    public async Task ExpandAsync_WhenLinuxUnicodeFallbackNeedsHexLetters_UsesLayoutAwareHexInput()
    {
        var keyboardLayoutService = CreateLayout(static character => character switch
        {
            'u' => (35, false, false),
            '1' => (17, true, false),
            'f' => (48, false, true),
            '6' => (32, false, false),
            '4' => (25, true, false),
            '2' => (99, false, false),
            _ => null,
        });
        var inputSimulator = new TestInputSimulator();
        await using var executor = CreateExecutor(CreateClipboard(isSupported: false), keyboardLayoutService, inputSimulator);

        var expansion = new TextExpansionEntry(":emoji", "🙂", insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion, TestContext.Current.CancellationToken);

        // Trigger backspaces, the Ctrl+Shift+U prefix, the hex digits of U+1F642 (shift/AltGr held
        // where the layout needs them), then Enter to commit.
        int[] expected =
        [
            .. Enumerable.Repeat(InputEventCode.KEY_BACKSPACE, ":emoji".Length),
            InputEventCode.KEY_LEFTCTRL, InputEventCode.KEY_LEFTSHIFT, 35,
            InputEventCode.KEY_LEFTSHIFT, 17,
            InputEventCode.KEY_RIGHTALT, 48,
            32,
            InputEventCode.KEY_LEFTSHIFT, 25,
            99,
            InputEventCode.KEY_ENTER,
        ];
        Assert.Equal(expected, inputSimulator.PressedKeys);
    }

    [LinuxFact]
    public async Task ExpandAsync_WhenLinuxUnicodeFallbackMissingLowercaseHex_UsesUppercaseFallback()
    {
        var keyboardLayoutService = CreateLayout(static character => character switch
        {
            'u' => (35, false, false),
            '1' => (17, false, false),
            'F' => (52, true, false),
            '6' => (32, false, false),
            '4' => (25, false, false),
            '2' => (99, false, false),
            _ => null,
        });
        var inputSimulator = new TestInputSimulator();
        await using var executor = CreateExecutor(CreateClipboard(isSupported: false), keyboardLayoutService, inputSimulator);

        var expansion = new TextExpansionEntry(":emoji", "🙂", insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion, TestContext.Current.CancellationToken);

        Assert.Contains(52, inputSimulator.PressedKeys);
    }

    [LinuxFact]
    public async Task ExpandAsync_WhenLinuxUnicodeFallbackCannotTypeRequiredHexDigit_ReportsClearErrorWithoutErasingTrigger()
    {
        var inputSimulator = new TestInputSimulator();
        await using var executor = CreateExecutor(CreateClipboard(isSupported: false), CreateLayoutWithoutHexLetterF(), inputSimulator);

        var expansion = new TextExpansionEntry(":emoji", "🙂", insertionMode: TextInsertionMode.DirectTyping);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExpandAsync(expansion, TestContext.Current.CancellationToken));

        Assert.Equal(
            "Current keyboard layout cannot type Linux unicode hex digit 'f' or 'F' required for code point U+1F642.",
            failure.Message);
        Assert.Empty(inputSimulator.PressedKeys);
    }

    [LinuxFact]
    public async Task ExpandAsync_WhenPasteFallbackCannotTypeRequiredHexDigit_DoesNotEraseTrigger()
    {
        var inputSimulator = new TestInputSimulator();
        await using var executor = CreateExecutor(CreateClipboard(isSupported: false), CreateLayoutWithoutHexLetterF(), inputSimulator);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExpandAsync(new TextExpansionEntry(":emoji", "🙂"), TestContext.Current.CancellationToken));

        Assert.Empty(inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenDirectTypingSimulatorSupportsBatch_UsesSingleBatchedSequenceWithFastTimings()
    {
        var inputSimulator = new BatchedTestInputSimulator();
        await using var executor = CreateExecutor(CreateClipboard(isSupported: false), CreateAbLayout(), inputSimulator);

        var expansion = new TextExpansionEntry(":abbr", "aB", insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(KeyA, inputSimulator.PressedKeys);
        Assert.DoesNotContain(KeyShift, inputSimulator.PressedKeys);
        Assert.DoesNotContain(KeyB, inputSimulator.PressedKeys);
        InputSimulationStep[] expected =
        [
            new(0x01, KeyA, 1),
            new(0x00, 0, 0, 1_000),
            new(0x01, KeyA, 0),
            new(0x00, 0, 0),
            new(0x01, KeyShift, 1),
            new(0x00, 0, 0),
            new(0x01, KeyB, 1),
            new(0x00, 0, 0, 1_000),
            new(0x01, KeyB, 0),
            new(0x00, 0, 0),
            new(0x01, KeyShift, 0),
            new(0x00, 0, 0),
        ];
        Assert.Equal(expected, Assert.Single(inputSimulator.Batches));
    }

    [Fact]
    public async Task ExpandAsync_WhenDirectTypingMethodIsCompatible_UsesBatchedTransportWithCompatibleTimings()
    {
        var inputSimulator = new BatchedTestInputSimulator();
        await using var executor = CreateExecutor(CreateClipboard(isSupported: false), CreateAbLayout(), inputSimulator);

        var expansion = new TextExpansionEntry(
            ":abbr",
            "aB",
            insertionMode: TextInsertionMode.DirectTyping,
            directTypingMethod: DirectTypingMethod.CompatibleKeyByKey);

        await executor.ExpandAsync(expansion, TestContext.Current.CancellationToken);

        InputSimulationStep[] expected =
        [
            new(0x01, KeyA, 1),
            new(0x00, 0, 0, 2_000),
            new(0x01, KeyA, 0),
            new(0x00, 0, 0, 2_000),
            new(0x01, KeyShift, 1),
            new(0x00, 0, 0),
            new(0x00, 0, 0, 2_000),
            new(0x01, KeyB, 1),
            new(0x00, 0, 0, 2_000),
            new(0x01, KeyB, 0),
            new(0x00, 0, 0, 2_000),
            new(0x01, KeyShift, 0),
            new(0x00, 0, 0),
        ];
        Assert.Equal(expected, Assert.Single(inputSimulator.Batches));
    }

    [Fact]
    public async Task ExpandAsync_WhenBatchWouldExceedIpcLimit_FallsBackToPerKeyTyping()
    {
        var inputSimulator = new BatchedTestInputSimulator();
        await using var executor = CreateExecutor(CreateClipboard(isSupported: false), CreateLayout(static _ => (KeyA, false, false)), inputSimulator);

        var replacement = new string('a', 2049);
        var expansion = new TextExpansionEntry(":long", replacement, insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion, TestContext.Current.CancellationToken);

        Assert.Empty(inputSimulator.Batches);
        Assert.Equal(replacement.Length, inputSimulator.PressedKeys.Count(static key => key is KeyA));
    }

    [Fact]
    public async Task ExpandAsync_WhenBatchCannotRepresentText_FallsBackToUnicodeInput()
    {
        var inputSimulator = new BatchedUnicodeCapableTestInputSimulator();
        await using var executor = CreateExecutor(CreateClipboard(isSupported: false), CreateLayout(static _ => null), inputSimulator);

        var expansion = new TextExpansionEntry(":emoji", "🙂", insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion, TestContext.Current.CancellationToken);

        Assert.Empty(inputSimulator.Batches);
        Assert.Equal(["🙂"], inputSimulator.TypedText);
    }

    private static TextExpansionExecutor CreateExecutor(
        IClipboardService clipboardService,
        IKeyboardLayoutService keyboardLayoutService,
        IInputSimulator inputSimulator)
    {
        return new TextExpansionExecutor(clipboardService, keyboardLayoutService, () => inputSimulator, new PacedTimeProvider());
    }

    private static IClipboardService CreateClipboard(bool isSupported)
    {
        var clipboardService = Substitute.For<IClipboardService>();
        _ = clipboardService.IsSupported.Returns(isSupported);
        return clipboardService;
    }

    private static IKeyboardLayoutService CreateLayout(Func<char, (int KeyCode, bool Shift, bool AltGr)?> resolve)
    {
        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        _ = keyboardLayoutService.GetInputForChar(Arg.Any<char>()).Returns(callInfo => resolve(callInfo.Arg<char>()));
        return keyboardLayoutService;
    }

    private static IKeyboardLayoutService CreateAbLayout()
    {
        return CreateLayout(static character => character switch
        {
            'a' => (KeyA, false, false),
            'B' => (KeyB, true, false),
            _ => null,
        });
    }

    private static IKeyboardLayoutService CreateLayoutWithoutHexLetterF()
    {
        return CreateLayout(static character => character switch
        {
            'u' => (35, false, false),
            '1' => (17, false, false),
            '6' => (32, false, false),
            '4' => (25, false, false),
            '2' => (99, false, false),
            _ => null,
        });
    }
}
