
namespace CrossMacro.Core.Tests.Services.Playback;

public sealed class PlaybackTimingServiceTests
{
    [Fact]
    public async Task WaitAsync_WhenDelayIsZero_ReturnsImmediately()
    {
        // Arrange
        var service = new PlaybackTimingService();
        var pauseToken = new FakePauseToken();

        // Act
        var act = async () => await service.WaitAsync(0, pauseToken, CancellationToken.None);

        // Assert
        _ = await act.Should().NotThrowAsync();
        _ = pauseToken.WaitCallCount.Should().Be(0);
    }

    [Fact]
    public async Task WaitAsync_WhenPaused_InvokesPauseWaitAndCompletes()
    {
        // Arrange
        var service = new PlaybackTimingService();
        var pauseToken = new FakePauseToken { IsPaused = true };

        // Act
        await service.WaitAsync(10, pauseToken, CancellationToken.None);

        // Assert
        _ = pauseToken.WaitCallCount.Should().Be(1);
    }

    [Fact]
    public async Task WaitAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = new PlaybackTimingService();
        var pauseToken = new FakePauseToken();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.WaitAsync(100, pauseToken, cts.Token);

        // Assert
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
