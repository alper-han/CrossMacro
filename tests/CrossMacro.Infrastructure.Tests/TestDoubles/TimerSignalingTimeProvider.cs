namespace CrossMacro.Infrastructure.Tests.TestDoubles;

/// <summary>
/// Wraps a <see cref="FakeTimeProvider"/> and signals whenever the code under test schedules a timer,
/// so a test can advance virtual time only after the delay it is waiting for exists.
/// </summary>
internal sealed class TimerSignalingTimeProvider(FakeTimeProvider? clock = null) : TimeProvider
{
    public FakeTimeProvider Clock { get; } = clock ?? new FakeTimeProvider();

    public AsyncSignal TimerScheduled { get; } = new();

    public override DateTimeOffset GetUtcNow() => Clock.GetUtcNow();

    public override TimeZoneInfo LocalTimeZone => Clock.LocalTimeZone;

    public override long GetTimestamp() => Clock.GetTimestamp();

    public override long TimestampFrequency => Clock.TimestampFrequency;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = Clock.CreateTimer(callback, state, dueTime, period);
        TimerScheduled.Signal();
        return timer;
    }
}
