namespace CrossMacro.Infrastructure.Tests.Services.ScreenReading;

public sealed class ScreenReadPollingTests
{
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
