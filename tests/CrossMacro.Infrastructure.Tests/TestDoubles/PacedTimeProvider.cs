using System.Threading.Channels;

namespace CrossMacro.Infrastructure.Tests.TestDoubles;

/// <summary>
/// Virtual clock for code that interleaves short pacing delays with long operation timeouts.
/// Timers due within <see cref="PacingLimit"/> elapse immediately, so the code under test runs
/// without real waiting. Longer timers are timeouts: they stay pending until the test elapses them
/// with <see cref="ElapseTimeoutAsync"/>.
/// </summary>
internal sealed class PacedTimeProvider : TimeProvider
{
    public static readonly TimeSpan PacingLimit = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan FailureUpperBound = TimeSpan.FromSeconds(10);

    private readonly FakeTimeProvider _clock = new();
    private readonly Channel<TimeSpan> _timeouts = Channel.CreateUnbounded<TimeSpan>();
    private int _observedTimeouts;

    public override DateTimeOffset GetUtcNow() => _clock.GetUtcNow();

    public override long GetTimestamp() => _clock.GetTimestamp();

    public override long TimestampFrequency => _clock.TimestampFrequency;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = _clock.CreateTimer(callback, state, dueTime, period);
        if (dueTime > PacingLimit)
        {
            _ = _timeouts.Writer.TryWrite(dueTime);
        }
        else if (dueTime > TimeSpan.Zero)
        {
            ThreadPool.QueueUserWorkItem(
                static pacing => pacing.Clock.Advance(pacing.Delay),
                (Clock: _clock, Delay: dueTime),
                preferLocal: false);
        }

        return timer;
    }

    /// <summary>
    /// Waits until the code under test has registered its <paramref name="timeoutNumber"/>-th timeout
    /// (1-based, in registration order), then advances the clock by that timeout so it fires.
    /// Timeouts left pending by earlier operations fire harmlessly with it.
    /// </summary>
    public async Task ElapseTimeoutAsync(int timeoutNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutNumber, _observedTimeouts + 1);

        TimeSpan due = default;
        while (_observedTimeouts < timeoutNumber)
        {
            due = await _timeouts.Reader.ReadAsync(CancellationToken.None).AsTask()
                .WaitAsync(FailureUpperBound, TimeProvider.System, CancellationToken.None);
            _observedTimeouts++;
        }

        _clock.Advance(due);
    }
}
