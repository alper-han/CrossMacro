namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class CaptureTimestampsTests
{
    [Theory]
    [InlineData(12_345_678L, 10_000_000L, 1_234_567L)]
    [InlineData(9_999_999L, 10_000_000L, 999_999L)]
    [InlineData(50_000_123L, 10_000_000L, 5_000_012L)]
    public void ToMicroseconds_ConvertsStopwatchTicksWithoutLosingWholeSeconds(
        long timestamp,
        long frequency,
        long expected)
    {
        Assert.Equal(expected, CaptureTimestamps.ToMicroseconds(timestamp, frequency));
    }

    [Fact]
    public void ToMicroseconds_WhenFrequencyIsNotPositive_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CaptureTimestamps.ToMicroseconds(timestamp: 1, frequency: 0));
    }
}
