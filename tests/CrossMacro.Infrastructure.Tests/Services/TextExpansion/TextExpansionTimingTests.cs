using System.Threading.Channels;

namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

public sealed class TextExpansionTimingTests
{
    [Theory]
    [InlineData(TextInsertionMode.Paste, false)]
    [InlineData(TextInsertionMode.DirectTyping, false)]
    [InlineData(TextInsertionMode.Paste, true)]
    public async Task ExpandAsync_WaitsForInjectedClockBeforeInsertingReplacement(
        TextInsertionMode insertionMode,
        bool clipboardWriteFails)
    {
        var timeProvider = new ObservableTimeProvider();
        var clipboard = new MemoryClipboardService(clipboardWriteFails);
        var layout = Substitute.For<IKeyboardLayoutService>();
        _ = layout.GetInputForChar('a').Returns((30, false, false));

        var keyEvents = new ConcurrentQueue<(int Code, bool Pressed, DateTimeOffset At)>();
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.InitializeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        simulator.When(input => input.KeyPress(Arg.Any<int>(), Arg.Any<bool>()))
            .Do(call => keyEvents.Enqueue((call.Arg<int>(), call.Arg<bool>(), timeProvider.GetUtcNow())));

        await using var executor = new TextExpansionExecutor(clipboard, layout, () => simulator, timeProvider);
        using var cancellation = new CancellationTokenSource();
        var startedAt = timeProvider.GetUtcNow();
        var execution = executor.ExpandAsync(
            new TextExpansionEntry(":", "a", insertionMode: insertionMode),
            cancellation.Token);
        var usesClipboard = insertionMode is TextInsertionMode.Paste && !clipboardWriteFails;

        try
        {
            await timeProvider.WaitForDelayAsync(TimeSpan.FromMilliseconds(1));
            timeProvider.Clock.Advance(TimeSpan.FromMilliseconds(1));
            await timeProvider.WaitForDelayAsync(TimeSpan.FromMilliseconds(5));

            Assert.Equal(
                [(InputEventCode.KEY_BACKSPACE, true), (InputEventCode.KEY_BACKSPACE, false)],
                keyEvents.Select(static input => (input.Code, input.Pressed)).ToArray());

            timeProvider.Clock.Advance(TimeSpan.FromMilliseconds(4));
            Assert.Equal(2, keyEvents.Count);
            Assert.False(execution.IsCompleted);

            timeProvider.Clock.Advance(TimeSpan.FromMilliseconds(1));
            await timeProvider.WaitForDelayAsync(TimeSpan.FromMilliseconds(1));

            var insertionKey = usesClipboard ? InputEventCode.KEY_V : 30;
            var replacementPress = Assert.Single(keyEvents, input => input.Code == insertionKey && input.Pressed);
            Assert.Equal(startedAt.AddMilliseconds(6), replacementPress.At);
            Assert.DoesNotContain(keyEvents, input => input.Code == (usesClipboard ? 30 : InputEventCode.KEY_V));

            timeProvider.Clock.Advance(TimeSpan.FromMilliseconds(1));
            if (usesClipboard)
            {
                await timeProvider.WaitForDelayAsync(TimeSpan.FromMilliseconds(50));
                Assert.Equal("a", clipboard.Content);
                Assert.Equal(["a"], clipboard.WriteAttempts.ToArray());

                timeProvider.Clock.Advance(TimeSpan.FromMilliseconds(49));
                Assert.Equal("a", clipboard.Content);
                Assert.False(execution.IsCompleted);
                timeProvider.Clock.Advance(TimeSpan.FromMilliseconds(1));
            }
            else
            {
                await timeProvider.WaitForDelayAsync(TimeSpan.FromMilliseconds(1));
                timeProvider.Clock.Advance(TimeSpan.FromMilliseconds(1));
            }

            await execution.WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: CancellationToken.None);
            Assert.Equal("original", clipboard.Content);
            string[] expectedWrites = clipboardWriteFails ? ["a"] : [];
            if (usesClipboard)
            {
                expectedWrites = ["a", "original"];
            }
            Assert.Equal(expectedWrites, clipboard.WriteAttempts.ToArray(), StringComparer.Ordinal);
        }
        finally
        {
            await cancellation.CancelAsync();
            try
            {
                await execution.WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: CancellationToken.None);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // Ensure a failed assertion cannot leave the executor waiting on virtual time.
            }
        }
    }

    private sealed class ObservableTimeProvider : TimeProvider
    {
        private readonly Channel<TimeSpan> _delays = Channel.CreateUnbounded<TimeSpan>();

        public FakeTimeProvider Clock { get; } = new();

        public override DateTimeOffset GetUtcNow() => Clock.GetUtcNow();

        public override long GetTimestamp() => Clock.GetTimestamp();

        public override long TimestampFrequency => Clock.TimestampFrequency;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = Clock.CreateTimer(callback, state, dueTime, period);
            // Clipboard operation timeouts do not participate in the insertion sequence.
            if (dueTime > TimeSpan.Zero && dueTime <= TimeSpan.FromMilliseconds(50))
            {
                _ = _delays.Writer.TryWrite(dueTime);
            }

            return timer;
        }

        public async Task WaitForDelayAsync(TimeSpan expectedDelay)
        {
            var actualDelay = await _delays.Reader.ReadAsync(CancellationToken.None).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), timeProvider: TimeProvider.System, cancellationToken: CancellationToken.None);
            Assert.Equal(expectedDelay, actualDelay);
        }
    }

    private sealed class MemoryClipboardService(bool rejectWrites) : IClipboardService, IClipboardWriteReadbackCapability
    {
        private string _content = "original";

        public bool IsSupported => true;

        public bool GuaranteesImmediateReadback => true;

        public string Content => Volatile.Read(ref _content);

        public ConcurrentQueue<string> WriteAttempts { get; } = new();

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(Content);
        }

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteAttempts.Enqueue(text);
            if (rejectWrites)
            {
                return Task.FromException(new InvalidOperationException("Clipboard writes are unavailable."));
            }

            Volatile.Write(ref _content, text);
            return Task.CompletedTask;
        }
    }
}
