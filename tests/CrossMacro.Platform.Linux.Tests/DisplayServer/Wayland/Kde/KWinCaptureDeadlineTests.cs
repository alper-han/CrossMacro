namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.Kde;

public sealed class KWinCaptureDeadlineTests
{
    [Fact]
    public async Task RunAsync_WhenTheOperationStallsPastTheTimeout_ThrowsTimeout()
    {
        var options = new ScreenReadOptions(timeout: TimeSpan.FromMilliseconds(50));

        _ = await Assert.ThrowsAsync<TimeoutException>(() => KWinCaptureDeadline.RunAsync(
            options,
            TimeProvider.System,
            static async token =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, token);
                return 0;
            }));
    }

    [Fact]
    public async Task RunAsync_WhenTheCallerCancels_ThrowsCancellationAndNotTimeout()
    {
        using var cancellation = new CancellationTokenSource();
        var options = new ScreenReadOptions(timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellation.Token);

        var run = KWinCaptureDeadline.RunAsync(
            options,
            TimeProvider.System,
            static async token =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, token);
                return 0;
            });
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task RunAsync_PassesTheDeadlineTokenToEveryStep()
    {
        var options = new ScreenReadOptions(timeout: TimeSpan.FromMinutes(5));

        var linkedTokenCanCancel = await KWinCaptureDeadline.RunAsync(
            options,
            TimeProvider.System,
            static token => Task.FromResult(token.CanBeCanceled));

        Assert.True(linkedTokenCanCancel);
    }

    [Fact]
    public async Task RunAsync_WithoutTimeout_UsesTheCallerToken()
    {
        using var cancellation = new CancellationTokenSource();
        var options = new ScreenReadOptions(cancellationToken: cancellation.Token);

        var received = await KWinCaptureDeadline.RunAsync(
            options,
            TimeProvider.System,
            static token => Task.FromResult(token));

        Assert.Equal(cancellation.Token, received);
    }

    [Fact]
    public async Task RunAsync_ReturnsTheOperationResultWithinTheTimeout()
    {
        var options = new ScreenReadOptions(timeout: TimeSpan.FromMinutes(5));

        var result = await KWinCaptureDeadline.RunAsync(options, TimeProvider.System, static _ => Task.FromResult(42));

        Assert.Equal(42, result);
    }
}
