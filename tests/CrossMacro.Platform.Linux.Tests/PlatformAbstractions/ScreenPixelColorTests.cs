namespace CrossMacro.Platform.Linux.Tests.PlatformAbstractions;

public class ScreenPixelColorTests
{
    [Theory]
    [InlineData("FF0000", 255, 0, 0, "FF0000")]
    [InlineData("00ff80", 0, 255, 128, "00FF80")]
    [InlineData("abcdef", 171, 205, 239, "ABCDEF")]
    public void Parse_NormalizesValidRgbLiteral(string value, byte red, byte green, byte blue, string canonical)
    {
        var color = ScreenPixelColor.Parse(value);

        Assert.Equal(red, color.R);
        Assert.Equal(green, color.G);
        Assert.Equal(blue, color.B);
        Assert.Equal(canonical, color.ToString());
    }

    [Theory]
    [InlineData("FFF")]
    [InlineData("FFFFFFFF")]
    [InlineData("GG0000")]
    [InlineData("#FF0000")]
    [InlineData("0xFF0000")]
    [InlineData("")]
    public void TryParse_RejectsInvalidRgbLiteral(string value)
    {
        var parsed = ScreenPixelColor.TryParse(value, out var color);

        Assert.False(parsed);
        Assert.Equal(default, color);
    }

    [Fact]
    public void Parse_ThrowsForInvalidRgbLiteral()
    {
        var exception = Assert.Throws<FormatException>(() => ScreenPixelColor.Parse("#FF0000"));

        Assert.Contains("6 hexadecimal RGB", exception.Message, StringComparison.Ordinal);
    }
}
