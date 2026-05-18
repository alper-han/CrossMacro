using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreLogging = CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.TextExpansion;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.TextExpansion;
using CrossMacro.TestInfrastructure;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class TextExpansionPrivacyTests
{
    [Fact]
    public async Task ExpandAsync_WhenClipboardBackupIsEmpty_RestoresEmptyClipboard()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(true);
        clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<string?>(string.Empty), // Backup
            Task.FromResult<string?>("replacement"), // Verification
            Task.FromResult<string?>("replacement")); // Restore guard check
        clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var restoreCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        clipboardService.When(x => x.SetTextAsync(string.Empty, Arg.Any<CancellationToken>()))
            .Do(_ => restoreCalled.TrySetResult(true));

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => new TestInputSimulator());

        var expansion = new TextExpansion(":a", "replacement");

        await executor.ExpandAsync(expansion);
        await restoreCalled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await clipboardService.Received(1).SetTextAsync("replacement", Arg.Any<CancellationToken>());
        await clipboardService.Received(1).SetTextAsync(string.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpandAsync_WhenPasteModeSucceeds_PreparesClipboardBeforeDeletingTrigger()
    {
        var clipboardService = new RecordingClipboardService(
            backupValue: "old-value",
            verificationValue: "replacement",
            restoreGuardValue: "replacement");
        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        var inputSimulator = new RecordingInputSimulator(clipboardService.Events);
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":a", "replacement");

        await executor.ExpandAsync(expansion);

        var events = clipboardService.Events.ToArray();
        Assert.True(Array.IndexOf(events, "clipboard:get:1") < Array.IndexOf(events, "clipboard:set:replacement"));
        Assert.True(Array.IndexOf(events, "clipboard:set:replacement") < Array.IndexOf(events, $"input:key:{InputEventCode.KEY_BACKSPACE}"));
        Assert.True(Array.IndexOf(events, $"input:key:{InputEventCode.KEY_LEFTCTRL}") < Array.IndexOf(events, $"input:key:{InputEventCode.KEY_V}"));
        Assert.True(Array.IndexOf(events, $"input:key:{InputEventCode.KEY_BACKSPACE}") < Array.IndexOf(events, $"input:key:{InputEventCode.KEY_V}"));
        Assert.True(Array.IndexOf(events, $"input:key:{InputEventCode.KEY_V}") < Array.IndexOf(events, "clipboard:set:old-value"));
    }

    [Fact]
    public async Task ExpandAsync_WhenStandardPasteUsesMetaModifier_UsesMetaPasteShortcut()
    {
        var clipboardService = new RecordingClipboardService(
            backupValue: "old-value",
            verificationValue: "replacement",
            restoreGuardValue: "replacement");
        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        var inputSimulator = new MetaPasteRecordingInputSimulator(clipboardService.Events);
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":a", "replacement");

        await executor.ExpandAsync(expansion);

        var events = clipboardService.Events.ToArray();
        Assert.DoesNotContain($"input:key:{InputEventCode.KEY_LEFTCTRL}", events);
        Assert.True(Array.IndexOf(events, $"input:key:{InputEventCode.KEY_LEFTMETA}") < Array.IndexOf(events, $"input:key:{InputEventCode.KEY_V}"));
        Assert.True(Array.IndexOf(events, $"input:key:{InputEventCode.KEY_V}") < Array.IndexOf(events, "clipboard:set:old-value"));
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardChangesAfterPaste_DoesNotRestoreOldClipboard()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(true);
        var restoreCheckReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var getTextCalls = 0;
        clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<string?>("old-value"),
            Task.FromResult<string?>("replacement"),
            Task.FromResult<string?>("user-new-copy"))
            .AndDoes(_ =>
            {
                if (Interlocked.Increment(ref getTextCalls) == 3)
                {
                    restoreCheckReached.TrySetResult(true);
                }
            }); // Restore guard check
        clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => new TestInputSimulator());

        var expansion = new TextExpansion(":a", "replacement");

        await executor.ExpandAsync(expansion);
        await restoreCheckReached.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await clipboardService.Received(1).SetTextAsync("replacement", Arg.Any<CancellationToken>());
        await clipboardService.DidNotReceive().SetTextAsync("old-value", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpandAsync_WhenRestoreGuardReadFails_DoesNotRestoreOldClipboard()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(true);
        var restoreCheckReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var getTextCalls = 0;
        clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<string?>("old-value"),
            Task.FromResult<string?>("replacement"),
            Task.FromResult<string?>(null))
            .AndDoes(_ =>
            {
                if (Interlocked.Increment(ref getTextCalls) == 3)
                {
                    restoreCheckReached.TrySetResult(true);
                }
            });
        clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => new TestInputSimulator());

        var expansion = new TextExpansion(":a", "replacement");

        await executor.ExpandAsync(expansion);
        await restoreCheckReached.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await clipboardService.Received(1).SetTextAsync("replacement", Arg.Any<CancellationToken>());
        await clipboardService.DidNotReceive().SetTextAsync("old-value", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardWriteTimesOut_CancelsWriteAndFallsBackToDirectTyping()
    {
        var clipboardService = new BlockingClipboardService();
        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        var inputSimulator = new UnicodeCapableTestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":a", "replacement");

        await executor.ExpandAsync(expansion);

        Assert.True(clipboardService.SetCancellationObserved);
        Assert.Equal("replacement", string.Concat(inputSimulator.TypedText));
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardVerificationDoesNotMatch_SkipsPasteAndFallsBackToDirectTyping()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(true);
        clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<string?>("old-value"),
            Task.FromResult<string?>("stale-host-clipboard"));
        clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        var inputSimulator = new UnicodeCapableTestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":a", "replacement");

        await executor.ExpandAsync(expansion);

        Assert.Equal("replacement", string.Concat(inputSimulator.TypedText));
        Assert.DoesNotContain(InputEventCode.KEY_V, inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardVerificationFailsAfterWrite_RestoresOldClipboardBeforeDirectFallback()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(true);
        clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<string?>("old-value"),
            Task.FromResult<string?>("stale-host-clipboard"),
            Task.FromResult<string?>("replacement"));
        clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        var inputSimulator = new UnicodeCapableTestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":a", "replacement");

        await executor.ExpandAsync(expansion);

        await clipboardService.Received(1).SetTextAsync("replacement", Arg.Any<CancellationToken>());
        await clipboardService.Received(1).SetTextAsync("old-value", Arg.Any<CancellationToken>());
        Assert.Equal("replacement", string.Concat(inputSimulator.TypedText));
        Assert.DoesNotContain(InputEventCode.KEY_V, inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardVerificationFailsAndClipboardChanged_DoesNotRestoreOldClipboard()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(true);
        clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<string?>("old-value"),
            Task.FromResult<string?>("stale-host-clipboard"),
            Task.FromResult<string?>("user-new-copy"));
        clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        var inputSimulator = new UnicodeCapableTestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":a", "replacement");

        await executor.ExpandAsync(expansion);

        await clipboardService.Received(1).SetTextAsync("replacement", Arg.Any<CancellationToken>());
        await clipboardService.DidNotReceive().SetTextAsync("old-value", Arg.Any<CancellationToken>());
        Assert.Equal("replacement", string.Concat(inputSimulator.TypedText));
        Assert.DoesNotContain(InputEventCode.KEY_V, inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardVerificationFailsAndDirectFallbackUnsupported_DoesNotEraseTrigger()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(true);
        clipboardService.GetTextAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<string?>("old-value"),
            Task.FromResult<string?>("stale-host-clipboard"));
        clipboardService.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns(default((int KeyCode, bool Shift, bool AltGr)?));
        var inputSimulator = new TestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":emoji", "🙂");

        await executor.ExpandAsync(expansion);

        Assert.Empty(inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardVerificationReadNeverCompletes_SkipsPasteAndFallsBackToDirectTyping()
    {
        var clipboardService = new NonCompletingReadAfterWriteClipboardService();
        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        var inputSimulator = new UnicodeCapableTestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":a", "replacement");

        await executor.ExpandAsync(expansion);

        Assert.True(clipboardService.VerificationReadStarted);
        Assert.Equal("replacement", string.Concat(inputSimulator.TypedText));
        Assert.DoesNotContain(InputEventCode.KEY_V, inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardReadBlocksBeforeReturningTask_TimesOutAndFallsBackToDirectTyping()
    {
        var clipboardService = new SynchronouslyBlockingReadClipboardService();
        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        var inputSimulator = new UnicodeCapableTestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":a", "replacement");

        await executor.ExpandAsync(expansion);

        Assert.True(clipboardService.ReadStarted);
        Assert.Equal("replacement", string.Concat(inputSimulator.TypedText));
        Assert.DoesNotContain(InputEventCode.KEY_V, inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task TriggerDetection_ShouldNotLogTriggerOrReplacementText()
    {
        var trigger = $"trigger-{Guid.NewGuid():N}";
        var replacement = $"replacement-{Guid.NewGuid():N}";

        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Current.Returns(new AppSettings { EnableTextExpansion = true });

        var storageService = Substitute.For<ITextExpansionStorageService>();
        var inputCapture = Substitute.For<IInputCapture>();
        inputCapture.ProviderName.Returns("test");
        var inputProcessor = Substitute.For<IInputProcessor>();
        var bufferState = Substitute.For<ITextBufferState>();
        var executor = Substitute.For<ITextExpansionExecutor>();

        var match = new TextExpansion(trigger, replacement);
        storageService.GetCurrent().Returns(new List<TextExpansion> { match });
        bufferState.TryGetMatch(Arg.Any<IEnumerable<TextExpansion>>(), out Arg.Any<TextExpansion?>())
            .Returns(callInfo =>
            {
                callInfo[1] = match;
                return true;
            });

        var executorCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        executor.ExpandAsync(match).Returns(_ =>
        {
            executorCalled.TrySetResult(true);
            return Task.CompletedTask;
        });

        var originalLogger = Log.Logger;
        var sink = new TestSink();
        try
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Sink(sink)
                .CreateLogger();

            using var service = new TextExpansionService(
                settingsService,
                storageService,
                () => inputCapture,
                inputProcessor,
                bufferState,
                executor);

            service.Start();
            inputProcessor.CharacterReceived += Raise.Event<Action<char>>('x');

            await executorCalled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            Log.Logger = originalLogger;
        }

        var rendered = sink.Events.Select(static e => e.RenderMessage()).ToArray();
        Assert.DoesNotContain(rendered, message => message.Contains(trigger, StringComparison.Ordinal));
        Assert.DoesNotContain(rendered, message => message.Contains(replacement, StringComparison.Ordinal));
    }

    [Fact]
    public async Task FallbackTyping_ShouldNotLogReplacementText()
    {
        var replacement = $"secret-{Guid.NewGuid():N}";
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(false);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns((30, false, false));

        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => new TestInputSimulator());

        var expansion = new TextExpansion(":a", replacement);

        var originalLogger = Log.Logger;
        var sink = new TestSink();
        try
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Sink(sink)
                .CreateLogger();

            await executor.ExpandAsync(expansion);
        }
        finally
        {
            Log.Logger = originalLogger;
        }

        var rendered = sink.Events.Select(static e => e.RenderMessage()).ToArray();
        Assert.DoesNotContain(rendered, message => message.Contains(replacement, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExpandAsync_WhenDirectTypingMode_DoesNotTouchClipboard()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(true);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns((30, false, false));

        var inputSimulator = new TestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(
            ":a",
            "typed",
            method: PasteMethod.CtrlShiftV,
            insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion);

        await clipboardService.DidNotReceive().GetTextAsync(Arg.Any<CancellationToken>());
        await clipboardService.DidNotReceive().SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Contains(30, inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenDirectTypingNeedsUnicode_UsesSimulatorUnicodeCapability()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(true);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns(default((int KeyCode, bool Shift, bool AltGr)?));

        var inputSimulator = new UnicodeCapableTestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(
            ":emoji",
            "🙂",
            insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion);

        await clipboardService.DidNotReceive().GetTextAsync(Arg.Any<CancellationToken>());
        await clipboardService.DidNotReceive().SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Contains("🙂", inputSimulator.TypedText);
    }

    [Fact]
    public async Task ExpandAsync_WhenDirectTypingAndSimulatorSupportsUnicode_PrefersUnicodeOverKeyboardLayout()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(true);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns((30, false, false));

        var inputSimulator = new UnicodeCapableTestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(
            ":ascii",
            "typed",
            insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion);

        Assert.Equal("typed", string.Concat(inputSimulator.TypedText));
        Assert.DoesNotContain(30, inputSimulator.PressedKeys);
        keyboardLayoutService.DidNotReceiveWithAnyArgs().GetInputForChar(default);
    }

    [Fact]
    public async Task ExpandAsync_WhenPasteFallsBackToDirectTyping_UsesSimulatorUnicodeCapability()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(false);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns(default((int KeyCode, bool Shift, bool AltGr)?));

        var inputSimulator = new UnicodeCapableTestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":emoji", "🙂");

        await executor.ExpandAsync(expansion);

        await clipboardService.DidNotReceive().GetTextAsync(Arg.Any<CancellationToken>());
        await clipboardService.DidNotReceive().SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Contains("🙂", inputSimulator.TypedText);
    }

    [LinuxFact]
    public async Task ExpandAsync_WhenLinuxUnicodeFallbackNeedsHexLetters_UsesLayoutAwareHexInput()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(false);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns(callInfo => callInfo.Arg<char>() switch
            {
                'u' => (35, false, false),
                '1' => (17, true, false),
                'f' => (48, false, true),
                '6' => (32, false, false),
                '4' => (25, true, false),
                '2' => (99, false, false),
                _ => default((int KeyCode, bool Shift, bool AltGr)?)
            });

        var inputSimulator = new TestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":emoji", "🙂", insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion);

        Assert.Contains(35, inputSimulator.PressedKeys);
        Assert.Contains(17, inputSimulator.PressedKeys);
        Assert.Contains(48, inputSimulator.PressedKeys);
        Assert.Contains(32, inputSimulator.PressedKeys);
        Assert.Contains(25, inputSimulator.PressedKeys);
        Assert.Contains(99, inputSimulator.PressedKeys);
        keyboardLayoutService.Received().GetInputForChar('u');
        keyboardLayoutService.Received().GetInputForChar('f');
    }

    [LinuxFact]
    public async Task ExpandAsync_WhenLinuxUnicodeFallbackMissingLowercaseHex_UsesUppercaseFallback()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(false);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns(callInfo => callInfo.Arg<char>() switch
            {
                'u' => (35, false, false),
                '1' => (17, false, false),
                'F' => (52, true, false),
                '6' => (32, false, false),
                '4' => (25, false, false),
                '2' => (99, false, false),
                _ => default((int KeyCode, bool Shift, bool AltGr)?)
            });

        var inputSimulator = new TestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":emoji", "🙂", insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion);

        Assert.Contains(52, inputSimulator.PressedKeys);
        keyboardLayoutService.Received().GetInputForChar('f');
        keyboardLayoutService.Received().GetInputForChar('F');
    }

    [LinuxFact]
    public async Task ExpandAsync_WhenLinuxUnicodeFallbackCannotTypeRequiredHexDigit_DoesNotEraseTriggerAndLogsClearError()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(false);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns(callInfo => callInfo.Arg<char>() switch
            {
                'u' => (35, false, false),
                '1' => (17, false, false),
                '6' => (32, false, false),
                '4' => (25, false, false),
                '2' => (99, false, false),
                _ => default((int KeyCode, bool Shift, bool AltGr)?)
            });

        var inputSimulator = new TestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":emoji", "🙂", insertionMode: TextInsertionMode.DirectTyping);

        var logger = new TestCoreLogger();
        using var _ = CoreLogging.Log.PushLogger(logger);

        await executor.ExpandAsync(expansion);

        Assert.Contains(logger.Entries, static e =>
            e.Exception?.Message.Contains(
                "Current keyboard layout cannot type Linux unicode hex digit 'f' or 'F' required for code point U+1F642.",
                StringComparison.Ordinal) == true);
        Assert.Empty(inputSimulator.PressedKeys);
    }

    [Fact]
    public async Task ExpandAsync_WhenDirectTypingSimulatorSupportsBatch_UsesSingleBatchedSequence()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(false);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns(callInfo => callInfo.Arg<char>() switch
            {
                'a' => (30, false, false),
                'B' => (48, true, false),
                _ => default((int KeyCode, bool Shift, bool AltGr)?)
            });

        var inputSimulator = new BatchedTestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":abbr", "aB", insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion);

        Assert.DoesNotContain(30, inputSimulator.PressedKeys);
        Assert.DoesNotContain(42, inputSimulator.PressedKeys);
        Assert.DoesNotContain(48, inputSimulator.PressedKeys);
        Assert.Single(inputSimulator.Batches);

        var batch = inputSimulator.Batches[0];
        InputSimulationStep[] expected =
        [
            new(0x01, 30, 1),
            new(0x00, 0, 0, 1),
            new(0x01, 30, 0),
            new(0x00, 0, 0),
            new(0x01, 42, 1),
            new(0x00, 0, 0),
            new(0x01, 48, 1),
            new(0x00, 0, 0, 1),
            new(0x01, 48, 0),
            new(0x00, 0, 0),
            new(0x01, 42, 0),
            new(0x00, 0, 0)
        ];
        Assert.Equal(expected, batch);
    }

    [Fact]
    public async Task ExpandAsync_WhenBatchWouldExceedIpcLimit_FallsBackToPerKeyTyping()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(false);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns((30, false, false));

        var inputSimulator = new BatchedTestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var replacement = new string('a', 2049);
        var expansion = new TextExpansion(":long", replacement, insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion);

        Assert.Empty(inputSimulator.Batches);
        Assert.Equal(replacement.Length, inputSimulator.PressedKeys.Count(static key => key == 30));
    }

    [Fact]
    public async Task ExpandAsync_WhenBatchCannotRepresentText_FallsBackToUnicodeInput()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(false);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns(default((int KeyCode, bool Shift, bool AltGr)?));

        var inputSimulator = new BatchedUnicodeCapableTestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":emoji", "🙂", insertionMode: TextInsertionMode.DirectTyping);

        await executor.ExpandAsync(expansion);

        Assert.Empty(inputSimulator.Batches);
        Assert.Equal(["🙂"], inputSimulator.TypedText);
    }

    [LinuxFact]
    public async Task ExpandAsync_WhenPasteFallbackCannotTypeRequiredHexDigit_DoesNotEraseTrigger()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(false);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        keyboardLayoutService.GetInputForChar(Arg.Any<char>())
            .Returns(callInfo => callInfo.Arg<char>() switch
            {
                'u' => (35, false, false),
                '1' => (17, false, false),
                '6' => (32, false, false),
                '4' => (25, false, false),
                '2' => (99, false, false),
                _ => default((int KeyCode, bool Shift, bool AltGr)?)
            });

        var inputSimulator = new TestInputSimulator();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => inputSimulator);

        var expansion = new TextExpansion(":emoji", "🙂");

        await executor.ExpandAsync(expansion);

        Assert.Empty(inputSimulator.PressedKeys);
    }

    private sealed class TestSink : ILogEventSink
    {
        public ConcurrentBag<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    private sealed class TestCoreLogger : CoreLogging.ICoreLogger
    {
        public ConcurrentBag<TestCoreLogEntry> Entries { get; } = new();

        public bool IsEnabled(CoreLogging.CoreLogLevel level) => true;

        public void Verbose(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(null, messageTemplate, propertyValues));

        public void Verbose(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Debug(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(null, messageTemplate, propertyValues));

        public void Debug(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Information(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(null, messageTemplate, propertyValues));

        public void Information(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Warning(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(null, messageTemplate, propertyValues));

        public void Warning(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Error(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(null, messageTemplate, propertyValues));

        public void Error(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Fatal(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(null, messageTemplate, propertyValues));

        public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));
    }

    private sealed record TestCoreLogEntry(Exception? Exception, string MessageTemplate, object?[] PropertyValues);

    private sealed class BlockingClipboardService : IClipboardService
    {
        public bool IsSupported => true;

        public bool SetCancellationObserved { get; private set; }

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                SetCancellationObserved = true;
                throw;
            }
        }
    }

    private sealed class NonCompletingReadAfterWriteClipboardService : IClipboardService
    {
        private int _readCount;

        public bool IsSupported => true;

        public bool VerificationReadStarted { get; private set; }

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
        {
            var readCount = Interlocked.Increment(ref _readCount);
            if (readCount == 1)
            {
                return Task.FromResult<string?>("old-value");
            }

            VerificationReadStarted = true;
            return Task.Delay(TimeSpan.FromSeconds(30), cancellationToken)
                .ContinueWith(
                    _ => (string?)"replacement",
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }
    }

    private sealed class SynchronouslyBlockingReadClipboardService : IClipboardService
    {
        public bool IsSupported => true;

        public bool ReadStarted { get; private set; }

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
        {
            ReadStarted = true;
            Thread.Sleep(TimeSpan.FromSeconds(30));
            return Task.FromResult<string?>("replacement");
        }
    }

    private sealed class RecordingClipboardService : IClipboardService
    {
        private readonly Queue<string?> _readValues;

        public RecordingClipboardService(
            string? backupValue,
            string? verificationValue,
            string? restoreGuardValue)
        {
            _readValues = new Queue<string?>([backupValue, verificationValue, restoreGuardValue]);
        }

        public List<string> Events { get; } = [];

        public bool IsSupported => true;

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            Events.Add($"clipboard:set:{text}");
            return Task.CompletedTask;
        }

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
        {
            var readNumber = Events.Count(static item => item.StartsWith("clipboard:get:", StringComparison.Ordinal)) + 1;
            Events.Add($"clipboard:get:{readNumber}");
            return Task.FromResult(_readValues.Count > 0 ? _readValues.Dequeue() : null);
        }
    }

    private sealed class RecordingInputSimulator : TestInputSimulator
    {
        private readonly List<string> _events;

        public RecordingInputSimulator(List<string> events)
        {
            _events = events;
        }

        public override void KeyPress(int keyCode, bool pressed)
        {
            if (pressed)
            {
                _events.Add($"input:key:{keyCode}");
            }

            base.KeyPress(keyCode, pressed);
        }
    }

    private sealed class MetaPasteRecordingInputSimulator : TestInputSimulator, IPlatformPasteShortcutProvider
    {
        private readonly List<string> _events;

        public MetaPasteRecordingInputSimulator(List<string> events)
        {
            _events = events;
        }

        public bool UsesMetaKeyForStandardPaste => true;

        public override void KeyPress(int keyCode, bool pressed)
        {
            if (pressed)
            {
                _events.Add($"input:key:{keyCode}");
            }

            base.KeyPress(keyCode, pressed);
        }
    }

    private class TestInputSimulator : IInputSimulator
    {
        public List<int> PressedKeys { get; } = new();

        public string ProviderName => "test";

        public bool IsSupported => true;

        public void Initialize(int screenWidth = 0, int screenHeight = 0)
        {
        }

        public void MoveAbsolute(int x, int y)
        {
        }

        public void MoveRelative(int dx, int dy)
        {
        }

        public void MouseButton(int button, bool pressed)
        {
        }

        public void Scroll(int delta, bool isHorizontal = false)
        {
        }

        public virtual void KeyPress(int keyCode, bool pressed)
        {
            if (pressed)
            {
                PressedKeys.Add(keyCode);
            }
        }

        public void Sync()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class UnicodeCapableTestInputSimulator : TestInputSimulator, IUnicodeTextInputSimulator
    {
        public bool SupportsUnicodeTextInput => true;

        public List<string> TypedText { get; } = new();

        public void TypeText(string text)
        {
            TypedText.Add(text);
        }
    }

    private class BatchedTestInputSimulator : TestInputSimulator, IBatchedInputSimulator
    {
        public List<InputSimulationStep[]> Batches { get; } = new();

        public bool SupportsBatchedInput => true;

        public void SimulateBatch(ReadOnlySpan<InputSimulationStep> steps)
        {
            Batches.Add(steps.ToArray());
        }
    }

    private sealed class BatchedUnicodeCapableTestInputSimulator : BatchedTestInputSimulator, IUnicodeTextInputSimulator
    {
        public bool SupportsUnicodeTextInput => true;

        public List<string> TypedText { get; } = new();

        public void TypeText(string text)
        {
            TypedText.Add(text);
        }
    }
}
