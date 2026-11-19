namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class ScrollNotchAccumulatorTests
{
    private const long WheelDelta = 120;

    [Theory]
    [InlineData(120L, 1)]
    [InlineData(-120L, -1)]
    [InlineData(240L, 2)]
    [InlineData(360L, 3)]
    public void Add_WhenDeltaIsWholeNotches_ReturnsThoseNotches(long delta, int expected)
    {
        var accumulator = new ScrollNotchAccumulator();

        Assert.Equal(expected, accumulator.Add(delta, WheelDelta));
    }

    [Fact]
    public void Add_WhenDeltaIsFractional_EmitsNothingUntilANotchBuildsUp()
    {
        var accumulator = new ScrollNotchAccumulator();

        Assert.Equal(0, accumulator.Add(40, WheelDelta));
        Assert.Equal(0, accumulator.Add(40, WheelDelta));
        Assert.Equal(1, accumulator.Add(40, WheelDelta));
        Assert.Equal(0, accumulator.Add(40, WheelDelta));
    }

    [Fact]
    public void Add_KeepsRemainderAcrossWholeNotches()
    {
        var accumulator = new ScrollNotchAccumulator();

        Assert.Equal(1, accumulator.Add(180, WheelDelta));
        Assert.Equal(1, accumulator.Add(60, WheelDelta));
        Assert.Equal(0, accumulator.Add(0, WheelDelta));
    }

    [Fact]
    public void Add_WhenDirectionReverses_DropsTheOppositeRemainder()
    {
        var accumulator = new ScrollNotchAccumulator();

        Assert.Equal(0, accumulator.Add(100, WheelDelta));
        Assert.Equal(0, accumulator.Add(-100, WheelDelta));
        Assert.Equal(-1, accumulator.Add(-20, WheelDelta));
    }

    [Fact]
    public void Add_WhenUnitsPerNotchIsNotPositive_Throws()
    {
        var accumulator = new ScrollNotchAccumulator();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => accumulator.Add(1, 0));
    }

    [Fact]
    public void Add_WhenDeltaExceedsIntRange_SaturatesNotches()
    {
        var accumulator = new ScrollNotchAccumulator();

        Assert.Equal(int.MaxValue, accumulator.Add(long.MaxValue, 1));
    }
}
