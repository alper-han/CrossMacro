namespace CrossMacro.Infrastructure.Tests.Services.ScreenReading;

public sealed class ScreenReadPollingTests
{
    [Fact]
    public async Task PollUntilMatchAsync_WhenTimeoutIsZeroEvaluatesOneAttempt()
    {
        var attempts = 0;
        var result = await ScreenReadPolling.PollUntilMatchAsync(
            (remaining, _) =>
            {
                attempts++;
                remaining.Should().Be(TimeSpan.Zero);
                return Task.FromResult(ScreenReadResultFactory.Success(42));
            },
            TimeSpan.Zero,
            TimeSpan.Zero,
            "polling canceled",
            timeoutFailure: null,
            CancellationToken.None);

        _ = result.IsSuccess.Should().BeTrue();
        _ = result.Value.Should().Be(42);
        _ = attempts.Should().Be(1);
    }

    [Fact]
    public async Task PollImageUntilConsistentAsync_WhenTimeoutIsZero_ReturnsFirstSuccessfulMatch()
    {
        var match = new ScreenImageMatch(new ScreenPoint(7, 9), 0.95, 12, 8);
        var attempts = 0;

        var result = await ScreenReadPolling.PollImageUntilConsistentAsync(
            (remaining, _) =>
            {
                attempts++;
                remaining.Should().Be(TimeSpan.Zero);
                return Task.FromResult(ScreenReadResultFactory.Success(match));
            },
            TimeSpan.Zero,
            TimeSpan.Zero,
            CancellationToken.None);

        _ = result.IsSuccess.Should().BeTrue();
        _ = result.Value.Should().Be(match);
        _ = attempts.Should().Be(1);
    }

    [Fact]
    public async Task PollImageUntilConsistentAsync_WhenTimeoutIsZero_ReturnsFirstNoMatch()
    {
        var attempts = 0;
        var noMatch = ScreenReadResultFactory.Failure<ScreenImageMatch>(
            ScreenReadErrorKind.CaptureTimeout,
            "not present");

        var result = await ScreenReadPolling.PollImageUntilConsistentAsync(
            (_, _) =>
            {
                attempts++;
                return Task.FromResult(noMatch);
            },
            TimeSpan.Zero,
            TimeSpan.Zero,
            CancellationToken.None);

        _ = result.IsSuccess.Should().BeFalse();
        _ = result.ErrorKind.Should().Be(ScreenReadErrorKind.CaptureTimeout);
        _ = result.ErrorMessage.Should().Be("not present");
        _ = attempts.Should().Be(1);
    }

    [Fact]
    public void GetDelay_NeverExceedsRemainingTimeoutBudget()
    {
        var deadline = TimeProvider.System.GetUtcNow() + TimeSpan.FromMilliseconds(10);

        var delay = ScreenReadPolling.GetDelay(deadline, TimeSpan.FromSeconds(5));

        _ = delay.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        _ = delay.Should().BeLessThanOrEqualTo(TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void GetDelay_WhenDeadlineHasPassed_ReturnsZero()
    {
        var deadline = TimeProvider.System.GetUtcNow() - TimeSpan.FromMilliseconds(1);

        _ = ScreenReadPolling.GetDelay(deadline, TimeSpan.FromSeconds(1)).Should().Be(TimeSpan.Zero);
    }
}
