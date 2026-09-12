
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

    [Theory]
    [InlineData("break", true)]
    [InlineData(" BREAK ", true)]
    [InlineData("break-now", false)]
    [InlineData("continue", false)]
    public void IsBreakCommand_RequiresExactTokenIgnoringCaseAndWhitespace(string step, bool expected)
    {
        _ = RunScriptSyntax.IsBreakCommand(step).Should().Be(expected);
    }

    [Theory]
    [InlineData("continue", true)]
    [InlineData(" Continue ", true)]
    [InlineData("continue-now", false)]
    [InlineData("break", false)]
    public void IsContinueCommand_RequiresExactTokenIgnoringCaseAndWhitespace(string step, bool expected)
    {
        _ = RunScriptSyntax.IsContinueCommand(step).Should().Be(expected);
    }

    [Theory]
    [InlineData("}", true)]
    [InlineData(" } ", true)]
    [InlineData("{", false)]
    [InlineData("}}", false)]
    public void IsBlockEndToken_RequiresExactClosingBrace(string step, bool expected)
    {
        _ = RunScriptSyntax.IsBlockEndToken(step).Should().Be(expected);
    }

    [Theory]
    [InlineData("else {", true)]
    [InlineData(" ELSE   { ", true)]
    [InlineData("else{", false)]
    [InlineData("else { extra", false)]
    [InlineData("if {", false)]
    public void IsElseHeader_RequiresElseAndStandaloneBrace(string step, bool expected)
    {
        _ = RunScriptSyntax.IsElseHeader(step).Should().Be(expected);
    }

    [Theory]
    [InlineData("current", true)]
    [InlineData(" CURRENT ", true)]
    [InlineData("current-position", false)]
    [InlineData("", false)]
    public void IsCurrentPositionToken_RequiresExactTokenIgnoringCaseAndWhitespace(string token, bool expected)
    {
        _ = RunScriptSyntax.IsCurrentPositionToken(token).Should().Be(expected);
    }

    [Theory]
    [InlineData("pixelcolor $x", true)]
    [InlineData("IMAGESEARCH image", true)]
    [InlineData("waitimage", true)]
    [InlineData("pixelcolorful", false)]
    [InlineData("window title", false)]
    [InlineData("", false)]
    public void IsScreenReadingStep_RequiresKnownCommandTokenBoundary(string step, bool expected)
    {
        _ = RunScriptSyntax.IsScreenReadingStep(step).Should().Be(expected);
    }

    [Theory]
    [InlineData("pixelcolor", true)]
    [InlineData(" WAITCOLOR ", true)]
    [InlineData("waitcolor now", false)]
    [InlineData("pixelcolorful", false)]
    public void IsScreenReadingCommandToken_RequiresExactKnownToken(string token, bool expected)
    {
        _ = RunScriptSyntax.IsScreenReadingCommandToken(token).Should().Be(expected);
    }

    [Theory]
    [InlineData("window title", true, false, false, false, false)]
    [InlineData("clipboard get $x", false, true, false, false, false)]
    [InlineData("shell echo hi", false, false, true, false, false)]
    [InlineData("screenshot out.png", false, false, false, true, false)]
    [InlineData("mouse position $x $y", false, false, false, false, true)]
    [InlineData("windowed title", false, false, false, false, false)]
    [InlineData("clipboardish", false, false, false, false, false)]
    public void CommandStepPredicates_RequireCommandTokenBoundary(
        string step,
        bool expectedWindow,
        bool expectedClipboard,
        bool expectedShell,
        bool expectedScreenshot,
        bool expectedMousePosition)
    {
        _ = RunScriptSyntax.IsWindowStep(step).Should().Be(expectedWindow);
        _ = RunScriptSyntax.IsClipboardStep(step).Should().Be(expectedClipboard);
        _ = RunScriptSyntax.IsShellStep(step).Should().Be(expectedShell);
        _ = RunScriptSyntax.IsScreenshotStep(step).Should().Be(expectedScreenshot);
        _ = RunScriptSyntax.IsMousePositionStep(step).Should().Be(expectedMousePosition);
    }
}
