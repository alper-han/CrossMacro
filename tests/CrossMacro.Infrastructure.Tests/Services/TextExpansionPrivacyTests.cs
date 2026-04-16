using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        clipboardService.GetTextAsync().Returns(
            Task.FromResult<string?>(string.Empty), // Backup
            Task.FromResult<string?>("replacement")); // Restore guard check
        clipboardService.SetTextAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

        var restoreCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        clipboardService.When(x => x.SetTextAsync(string.Empty))
            .Do(_ => restoreCalled.TrySetResult(true));

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => new TestInputSimulator());

        var expansion = new TextExpansion(":a", "replacement");

        await executor.ExpandAsync(expansion);
        await restoreCalled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await clipboardService.Received(1).SetTextAsync("replacement");
        await clipboardService.Received(1).SetTextAsync(string.Empty);
    }

    [Fact]
    public async Task ExpandAsync_WhenClipboardChangesAfterPaste_DoesNotRestoreOldClipboard()
    {
        var clipboardService = Substitute.For<IClipboardService>();
        clipboardService.IsSupported.Returns(true);
        clipboardService.GetTextAsync().Returns(
            Task.FromResult<string?>("old-value"), // Backup
            Task.FromResult<string?>("user-new-copy")); // Restore guard check
        clipboardService.SetTextAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

        var keyboardLayoutService = Substitute.For<IKeyboardLayoutService>();
        var executor = new TextExpansionExecutor(
            clipboardService,
            keyboardLayoutService,
            () => new TestInputSimulator());

        var expansion = new TextExpansion(":a", "replacement");

        await executor.ExpandAsync(expansion);
        await Task.Delay(1500);

        await clipboardService.Received(1).SetTextAsync("replacement");
        await clipboardService.DidNotReceive().SetTextAsync("old-value");
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

        await clipboardService.DidNotReceive().GetTextAsync();
        await clipboardService.DidNotReceive().SetTextAsync(Arg.Any<string>());
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

        await clipboardService.DidNotReceive().GetTextAsync();
        await clipboardService.DidNotReceive().SetTextAsync(Arg.Any<string>());
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

        await clipboardService.DidNotReceive().GetTextAsync();
        await clipboardService.DidNotReceive().SetTextAsync(Arg.Any<string>());
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

        Assert.Contains(sink.Events, static e =>
            e.Exception?.Message.Contains(
                "Current keyboard layout cannot type Linux unicode hex digit 'f' or 'F' required for code point U+1F642.",
                StringComparison.Ordinal) == true);
        Assert.Empty(inputSimulator.PressedKeys);
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

        public void KeyPress(int keyCode, bool pressed)
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
}
