namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSScrollTranslatorTests
{
    private const long Line = MacOSScrollTranslator.FixedPointUnitsPerLine;

    [Theory]
    [InlineData(3L, 0L, 3L * Line)]
    [InlineData(-2L, 0L, -2L * Line)]
    [InlineData(0L, Line / 2, Line / 2)]
    [InlineData(1L, Line / 4, Line / 4)]
    [InlineData(0L, 0L, 0L)]
    public void ToFixedPointLines_PrefersFractionalLinesOverTheRoundedLineCount(
        long lineDelta,
        long fixedPointDelta,
        long expected)
    {
        Assert.Equal(expected, MacOSScrollTranslator.ToFixedPointLines(lineDelta, fixedPointDelta));
    }

    [Fact]
    public void VerticalNotches_WhenMouseWheelReportsLines_ReturnsThoseLines()
    {
        var translator = new MacOSScrollTranslator();

        Assert.Equal(1, translator.VerticalNotches(lineDelta: 1, fixedPointLineDelta: Line));
        Assert.Equal(-4, translator.VerticalNotches(lineDelta: -4, fixedPointLineDelta: -4 * Line));
    }

    [Fact]
    public void VerticalNotches_WhenTrackpadReportsFractionalLines_AccumulatesUntilALineIsReached()
    {
        var translator = new MacOSScrollTranslator();

        Assert.Equal(0, translator.VerticalNotches(lineDelta: 0, fixedPointLineDelta: Line * 4 / 10));
        Assert.Equal(0, translator.VerticalNotches(lineDelta: 0, fixedPointLineDelta: Line * 4 / 10));
        Assert.Equal(1, translator.VerticalNotches(lineDelta: 0, fixedPointLineDelta: Line * 4 / 10));
    }

    [Fact]
    public void VerticalNotches_WhenPixelHeavyTrackpadDeltaArrives_DoesNotReplayPixelsAsLines()
    {
        var translator = new MacOSScrollTranslator();

        Assert.Equal(0, translator.VerticalNotches(lineDelta: 0, fixedPointLineDelta: Line / 10));
    }

    [Fact]
    public void HorizontalNotches_FlipsCoreGraphicsSignSoPositiveIsRight()
    {
        var translator = new MacOSScrollTranslator();

        Assert.Equal(-2, translator.HorizontalNotches(lineDelta: 2, fixedPointLineDelta: 2 * Line));
        Assert.Equal(3, translator.HorizontalNotches(lineDelta: -3, fixedPointLineDelta: -3 * Line));
    }

    [Fact]
    public void Axes_KeepSeparateRemainders()
    {
        var translator = new MacOSScrollTranslator();

        Assert.Equal(0, translator.VerticalNotches(0, Line / 2));
        Assert.Equal(0, translator.HorizontalNotches(0, Line / 2));
        Assert.Equal(1, translator.VerticalNotches(0, Line / 2));
    }

    [Theory]
    [InlineData(3, false, 1u, 3, 0)]
    [InlineData(-2, false, 1u, -2, 0)]
    [InlineData(2, true, 2u, 0, -2)]
    [InlineData(-5, true, 2u, 0, 5)]
    [InlineData(int.MinValue, true, 2u, 0, int.MaxValue)]
    public void ToWheelValues_PlacesNotchesOnTheRequestedAxis(
        int notches,
        bool isHorizontal,
        uint expectedWheelCount,
        int expectedWheel1,
        int expectedWheel2)
    {
        var (wheelCount, wheel1, wheel2) = MacOSScrollTranslator.ToWheelValues(notches, isHorizontal);

        Assert.Equal(expectedWheelCount, wheelCount);
        Assert.Equal(expectedWheel1, wheel1);
        Assert.Equal(expectedWheel2, wheel2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HorizontalNotchesAndWheelValues_RoundTrip(bool isHorizontal)
    {
        var translator = new MacOSScrollTranslator();
        const int recordedNotches = 3;

        var (_, wheel1, wheel2) = MacOSScrollTranslator.ToWheelValues(recordedNotches, isHorizontal);
        long nativeLines = isHorizontal ? wheel2 : wheel1;
        int replayedNotches = isHorizontal
            ? translator.HorizontalNotches(nativeLines, nativeLines * Line)
            : translator.VerticalNotches(nativeLines, nativeLines * Line);

        Assert.Equal(recordedNotches, replayedNotches);
    }
}
