
namespace CrossMacro.Core.Tests.Services;

public sealed class RunScriptHeaderParserTests
{
    [Theory]
    [InlineData("repeat 5 {", "5")]
    [InlineData("repeat $a {", "$a")]
    [InlineData("repeat -1 {", "-1")]
    [InlineData("REPEAT 2 {", "2")]
    [InlineData("repeat 5+3 {", "5+3")]
    [InlineData("repeat $a / 2 {", "$a / 2")]
    [InlineData("repeat  $a   /   2  {", "$a / 2")]
    [InlineData("repeat $a / {", "$a /")]
    [InlineData("repeat 1 + 2 + 3 {", "1 + 2 + 3")]
    public void TryParseRepeatCountToken_WhenCountIsSimpleOrExpression_ReturnsRawTokens(string step, string expectedToken)
    {
        // The parser returns raw expression tokens; Core ScriptNumericExpression classifies them.
        var parsed = RunScriptHeaderParser.TryParseRepeatCountToken(step, out var countToken);

        _ = parsed.Should().BeTrue();
        _ = countToken.Should().Be(expectedToken);
    }

    [Theory]
    [InlineData("repeat 5{")]
    [InlineData("repeat {")]
    [InlineData("repeat 5")]
    [InlineData("repeat")]
    [InlineData("repeat5 {")]
    [InlineData("set x 1")]
    [InlineData("")]
    public void TryParseRepeatCountToken_WhenShapeIsNotRepeatHeader_ReturnsFalse(string step)
    {
        var parsed = RunScriptHeaderParser.TryParseRepeatCountToken(step, out _);

        _ = parsed.Should().BeFalse();
    }

    [Fact]
    public void TryParseForHeader_WhenSimpleSegments_ParsesAllFields()
    {
        var parsed = RunScriptHeaderParser.TryParseForHeader("for i from 1 to 3 {", out var header, out var error);

        _ = parsed.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = header!.VariableName.Should().Be("i");
        _ = header.StartToken.Should().Be("1");
        _ = header.EndToken.Should().Be("3");
        _ = header.StepToken.Should().BeNull();
        _ = header.HasExplicitStep.Should().BeFalse();
    }

    [Fact]
    public void TryParseForHeader_WhenSegmentsAreExpressions_ReturnsRawSegmentTokens()
    {
        var parsed = RunScriptHeaderParser.TryParseForHeader("for i from $start to $n * 2 {", out var header, out var error);

        _ = parsed.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = header!.StartToken.Should().Be("$start");
        _ = header.EndToken.Should().Be("$n * 2");
        _ = header.HasExplicitStep.Should().BeFalse();
    }

    [Fact]
    public void TryParseForHeader_WhenStepIsExpression_ReturnsRawStepTokens()
    {
        var parsed = RunScriptHeaderParser.TryParseForHeader("for i from 1 to 10 step $s + 1 {", out var header, out var error);

        _ = parsed.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = header!.StartToken.Should().Be("1");
        _ = header.EndToken.Should().Be("10");
        _ = header.StepToken.Should().Be("$s + 1");
        _ = header.HasExplicitStep.Should().BeTrue();
    }

    [Fact]
    public void TryParseForHeader_WhenVariablesShareKeywordNames_SigilDisambiguates()
    {
        var parsed = RunScriptHeaderParser.TryParseForHeader("for i from $from to $to step $step {", out var header, out var error);

        _ = parsed.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = header!.StartToken.Should().Be("$from");
        _ = header.EndToken.Should().Be("$to");
        _ = header.StepToken.Should().Be("$step");
        _ = header.HasExplicitStep.Should().BeTrue();
    }

    [Fact]
    public void TryParseForHeader_WhenBraceIsAdjacentToLastToken_KeepsLegacyLeniency()
    {
        var parsed = RunScriptHeaderParser.TryParseForHeader("for i from 1 to 3{", out var header, out var error);

        _ = parsed.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = header!.EndToken.Should().Be("3");
    }

    [Theory]
    [InlineData("for i 1 to 3 {")]
    [InlineData("for i from to 3 {")]
    [InlineData("for i from 1 to {")]
    [InlineData("for i from 1 to 3 step {")]
    [InlineData("for from 1 to 3 {")]
    public void TryParseForHeader_WhenShapeIsInvalid_ReturnsSyntaxError(string step)
    {
        var parsed = RunScriptHeaderParser.TryParseForHeader(step, out var header, out var error);

        _ = parsed.Should().BeTrue();
        _ = header.Should().BeNull();
        _ = error.Should().Be("Invalid for syntax. Expected: for <var> from <start> to <end> [step <n>] {");
    }

    [Fact]
    public void TryParseForHeader_WhenVariableNameIsInvalid_ReturnsNameError()
    {
        var parsed = RunScriptHeaderParser.TryParseForHeader("for 1bad from 1 to 3 {", out var header, out var error);

        _ = parsed.Should().BeTrue();
        _ = header.Should().BeNull();
        _ = error.Should().Be("Invalid loop variable name '1bad'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*");
    }

    [Fact]
    public void TryParseForHeader_WhenStepSegmentIsNotExpression_ReturnsRawTokensForCallerClassification()
    {
        var parsed = RunScriptHeaderParser.TryParseForHeader("for i from 1 to 3 step 2 extra {", out var header, out var error);

        _ = parsed.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = header!.StepToken.Should().Be("2 extra");
    }

    [Theory]
    [InlineData("fori from 1 to 3 {")]
    [InlineData("for i from 1 to 3")]
    [InlineData("repeat 3 {")]
    [InlineData("")]
    public void TryParseForHeader_WhenShapeIsNotForHeader_ReturnsFalse(string step)
    {
        var parsed = RunScriptHeaderParser.TryParseForHeader(step, out var header, out var error);

        _ = parsed.Should().BeFalse();
        _ = header.Should().BeNull();
        _ = error.Should().BeNull();
    }
}
