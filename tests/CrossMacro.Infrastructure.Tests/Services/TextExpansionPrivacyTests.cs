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
        clipboardService.GetTextAsync().Returns(Task.FromResult<string?>(string.Empty));
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

    private sealed class TestSink : ILogEventSink
    {
        public ConcurrentBag<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    private sealed class TestInputSimulator : IInputSimulator
    {
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
        }

        public void Sync()
        {
        }

        public void Dispose()
        {
        }
    }
}
