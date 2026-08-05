namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public sealed class SystemPlaybackTimingServiceTests
{
    [Fact]
    public async Task WaitAsync_WhenDelayIsZero_ReturnsImmediately()
    {
        var service = new SystemPlaybackTimingService();
        var pauseToken = new FakePauseToken();

        await service.WaitAsync(0, pauseToken, CancellationToken.None);

        _ = pauseToken.WaitCallCount.Should().Be(0);
    }

    [Fact]
    public async Task WaitAsync_WhenPaused_ResumesAfterPauseTokenCompletes()
    {
        var service = new SystemPlaybackTimingService();
        var pauseToken = new FakePauseToken { IsPaused = true };

        await service.WaitAsync(2, pauseToken, CancellationToken.None);

        _ = pauseToken.WaitCallCount.Should().Be(1);
    }

    [Fact]
    public async Task WaitAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var service = new SystemPlaybackTimingService();
        var pauseToken = new FakePauseToken();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = async () => await service.WaitAsync(100, pauseToken, cancellation.Token);

        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class FakePauseToken : IPlaybackPauseToken
    {
        public bool IsPaused { get; set; }
        public int WaitCallCount { get; private set; }

        public Task WaitIfPausedAsync(CancellationToken cancellationToken)
        {
            WaitCallCount++;
            IsPaused = false;
            return Task.CompletedTask;
        }
    }
}
