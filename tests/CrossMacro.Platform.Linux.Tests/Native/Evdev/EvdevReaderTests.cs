namespace CrossMacro.Platform.Linux.Tests.Native.Evdev;

public sealed class EvdevReaderTests
{
    [LinuxFact]
    public async Task StartStopStart_UsesFreshCancellationTokenPerSession()
    {
        var sessionTokens = new List<CancellationToken>();
        var firstStartSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStartSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeCalls = 0;
        var invocationCount = 0;

        await using var reader = new EvdevReader(
            "/dev/input/fake",
            "fake-device",
            static _ => 123,
            _ => closeCalls++,
            token =>
            {
                sessionTokens.Add(token);
                var invocation = Interlocked.Increment(ref invocationCount);
                if (invocation == 1)
                {
                    _ = firstStartSignal.TrySetResult(true);
                }
                else
                {
                    _ = secondStartSignal.TrySetResult(true);
                }

                return Task.Delay(Timeout.InfiniteTimeSpan, token);
            });

        reader.Start();
        _ = await firstStartSignal.Task.WaitAsync(TimeSpan.FromSeconds(1));

        reader.Stop();

        _ = Assert.Single(sessionTokens);
        Assert.True(sessionTokens[0].IsCancellationRequested);
        Assert.Equal(1, closeCalls);

        reader.Start();
        _ = await secondStartSignal.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(2, sessionTokens.Count);
        Assert.False(sessionTokens[1].IsCancellationRequested);

        reader.Stop();
    }

    [LinuxFact]
    public async Task DisposeAsync_WhenReaderLoopIsActive_CompletesAfterCancellation()
    {
        var loopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeCalls = 0;

        await using var reader = new EvdevReader(
            "/dev/input/fake",
            "fake-device",
            static _ => 456,
            _ => closeCalls++,
            token =>
            {
                _ = loopStarted.TrySetResult();
                return Task.Delay(Timeout.InfiniteTimeSpan, token);
            });

        reader.Start();
        await loopStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await reader.DisposeAsync();

        Assert.Equal(1, closeCalls);
        Assert.False(reader.IsListening);
    }
}
