namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class RunScriptScreenReadRuntimeTests
{
    private sealed class ImagePollingTimeProvider : TimeProvider
    {
        public FakeTimeProvider Clock { get; } = new();

        public TaskCompletionSource TimerCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override DateTimeOffset GetUtcNow() => Clock.GetUtcNow();

        public override long GetTimestamp() => Clock.GetTimestamp();

        public override long TimestampFrequency => Clock.TimestampFrequency;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = Clock.CreateTimer(callback, state, dueTime, period);
            _ = TimerCreated.TrySetResult();
            return timer;
        }
    }
}
