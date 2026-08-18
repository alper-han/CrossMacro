
namespace CrossMacro.Core.Tests.Services;

public sealed class RunScriptSyntaxTests
{
    [Fact]
    public void SplitQuotedTokens_SplitsWhitespaceAndKeepsQuotedValuesTogether()
    {
        var tokens = RunScriptSyntax.SplitQuotedTokens("  window   search title \"Code Editor\" result  ");

        _ = tokens.Should().Equal("window", "search", "title", "Code Editor", "result");
    }

    [Fact]
    public void SplitQuotedTokens_UnescapesBackslashesAndQuotesInsideQuotes()
    {
        var tokens = RunScriptSyntax.SplitQuotedTokens("window focus title 'it\\'s \\\\ ok'");

        _ = tokens.Should().Equal("window", "focus", "title", "it's \\ ok");
    }

    [Fact]
    public void SplitQuotedTokens_KeepsUnquotedBackslashesLiteral()
    {
        var tokens = RunScriptSyntax.SplitQuotedTokens(@"window focus title C:\Temp");

        _ = tokens.Should().Equal("window", "focus", "title", @"C:\Temp");
    }

    [Fact]
    public void SplitQuotedTokens_ThrowsForUnterminatedQuote()
    {
        Action split = () => RunScriptSyntax.SplitQuotedTokens("window focus title \"Code Editor");

        _ = split.Should().Throw<FormatException>().WithMessage("Unterminated quoted token.");
    }

    [Theory]
    [InlineData("abs", MouseCoordinateMode.Absolute, MouseCoordinateSpace.LogicalDesktop)]
    [InlineData("absolute", MouseCoordinateMode.Absolute, MouseCoordinateSpace.LogicalDesktop)]
    [InlineData("rel", MouseCoordinateMode.Relative, MouseCoordinateSpace.RawDevice)]
    [InlineData("relative", MouseCoordinateMode.Relative, MouseCoordinateSpace.RawDevice)]
    [InlineData("rel-logical", MouseCoordinateMode.Relative, MouseCoordinateSpace.LogicalDesktop)]
    [InlineData("relative-logical", MouseCoordinateMode.Relative, MouseCoordinateSpace.LogicalDesktop)]
    [InlineData("rel-raw", MouseCoordinateMode.Relative, MouseCoordinateSpace.RawDevice)]
    [InlineData("relative-raw", MouseCoordinateMode.Relative, MouseCoordinateSpace.RawDevice)]
    public void TryParseMouseMoveMode_WhenTokenIsSupported_ReturnsModeAndSpace(
        string token,
        MouseCoordinateMode expectedMode,
        MouseCoordinateSpace expectedSpace)
    {
        var parsed = RunScriptSyntax.TryParseMouseMoveMode(token, out var mode, out var space);

        _ = parsed.Should().BeTrue();
        _ = mode.Should().Be(expectedMode);
        _ = space.Should().Be(expectedSpace);
    }

    [Fact]
    public void TryParseMouseMoveMode_WhenTokenIsUnsupported_ReturnsFalse()
    {
        var parsed = RunScriptSyntax.TryParseMouseMoveMode("sideways", out _, out _);

        _ = parsed.Should().BeFalse();
    }

    [Theory]
    [InlineData(MouseCoordinateMode.Absolute, MouseCoordinateSpace.LogicalDesktop, "abs")]
    [InlineData(MouseCoordinateMode.Relative, MouseCoordinateSpace.LogicalDesktop, "rel-logical")]
    [InlineData(MouseCoordinateMode.Relative, MouseCoordinateSpace.RawDevice, "rel-raw")]
    public void ToMouseMoveModeToken_ReturnsCanonicalToken(
        MouseCoordinateMode mode,
        MouseCoordinateSpace space,
        string expected)
    {
        _ = RunScriptSyntax.ToMouseMoveModeToken(mode, space).Should().Be(expected);
    }
}
