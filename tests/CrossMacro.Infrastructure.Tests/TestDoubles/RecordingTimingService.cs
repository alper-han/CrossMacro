namespace CrossMacro.Infrastructure.Tests.TestDoubles;

internal sealed class RecordingTimingService : IPlaybackTimingService
{
    public List<double> WaitCalls { get; } = new();
    public TaskCompletionSource<bool>? WaitEntered { get; set; }
    public TaskCompletionSource<bool>? ContinueWait { get; set; }

    public async Task WaitAsync(double delayMilliseconds, IPlaybackPauseToken pauseToken, CancellationToken cancellationToken)
    {
        WaitCalls.Add(delayMilliseconds);
        _ = (WaitEntered?.TrySetResult(true));

        if (ContinueWait is not null)
        {
            _ = await ContinueWait.Task.WaitAsync(cancellationToken);
        }

        if (pauseToken.IsPaused)
        {
            await pauseToken.WaitIfPausedAsync(cancellationToken);
        }
    }
}
