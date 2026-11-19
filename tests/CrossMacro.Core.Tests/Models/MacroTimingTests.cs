namespace CrossMacro.Core.Tests.Models;

public sealed class MacroTimingTests
{
    [Theory]
    [InlineData("20", 20_000)]
    [InlineData("20ms", 20_000)]
    [InlineData("2.375ms", 2_375)]
    [InlineData("250us", 250)]
    [InlineData("250µs", 250)]
    [InlineData("  5 ms ", 5_000)]
    [InlineData("0", 0)]
    public void TryParseDurationMicroseconds_AcceptsLegacyMillisecondsAndExplicitUnits(string input, long expectedMicroseconds)
    {
        _ = MacroTiming.TryParseDurationMicroseconds(input, out var actual).Should().BeTrue();
        _ = actual.Should().Be(expectedMicroseconds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("-5ms")]
    [InlineData("0.5us")]
    [InlineData("1.2345ms")]
    [InlineData("9223372036854776ms")]
    [InlineData("9223372036854775808us")]
    public void TryParseDurationMicroseconds_RejectsInvalidFractionalAndOverflowingValues(string? input)
    {
        var parsed = MacroTiming.TryParseDurationMicroseconds(input, out var actual);

        _ = parsed.Should().BeFalse();
        _ = actual.Should().Be(0);
    }

    [Theory]
    [InlineData(0, "0ms")]
    [InlineData(250, "250us")]
    [InlineData(2_000, "2ms")]
    [InlineData(2_375, "2.375ms")]
    public void FormatDuration_UsesMillisecondsUnlessTheValueIsSubMillisecond(long microseconds, string expected)
    {
        _ = MacroTiming.FormatDuration(microseconds).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(2_000, "2")]
    [InlineData(250, "250us")]
    [InlineData(2_375, "2.375ms")]
    public void FormatScriptDuration_UsesUnitlessMillisecondsWhenExact(long microseconds, string expected)
    {
        _ = MacroTiming.FormatScriptDuration(microseconds).Should().Be(expected);
    }

    [Fact]
    public void Formatting_RejectsNegativeDurations()
    {
        _ = FluentActions.Invoking(() => MacroTiming.FormatDuration(-1)).Should().Throw<ArgumentOutOfRangeException>();
        _ = FluentActions.Invoking(() => MacroTiming.FormatScriptDuration(-1)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(2_375L)]
    [InlineData(250L)]
    [InlineData(20_000L)]
    public void FormattedDurations_RoundTripThroughTheParser(long microseconds)
    {
        _ = MacroTiming.TryParseDurationMicroseconds(MacroTiming.FormatDuration(microseconds), out var viaEditor).Should().BeTrue();
        _ = MacroTiming.TryParseDurationMicroseconds(MacroTiming.FormatScriptDuration(microseconds), out var viaScript).Should().BeTrue();
        _ = viaEditor.Should().Be(microseconds);
        _ = viaScript.Should().Be(microseconds);
    }

    [Theory]
    [InlineData(-1_500L, -1)]
    [InlineData(999L, 0)]
    [InlineData(12_345L, 12)]
    [InlineData(long.MaxValue, int.MaxValue)]
    [InlineData(long.MinValue, int.MinValue)]
    public void ToLegacyMilliseconds_TruncatesAndClampsToInt(long microseconds, int expected)
    {
        _ = MacroTiming.ToLegacyMilliseconds(microseconds).Should().Be(expected);
    }
}
