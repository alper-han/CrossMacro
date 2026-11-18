using CrossMacro.Infrastructure.Services.Scripting;

namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ScriptQuotingTests
{
    [Theory]
    [InlineData("\"a b\"", "a b")]
    [InlineData("'a b'", "a b")]
    [InlineData("  \"a b\"  ", "a b")]
    [InlineData("plain", "plain")]
    [InlineData("\"mismatch'", "\"mismatch'")]
    [InlineData("\"", "\"")]
    [InlineData("\"\"", "")]
    [InlineData("\"say \\\"hi\\\"\"", "say \"hi\"")]
    [InlineData("'it\\'s'", "it's")]
    [InlineData("\"back\\\\slash\"", "back\\slash")]
    [InlineData("\"\\\\\\\"\"", "\\\"")]
    [InlineData("\"keep \\n as is\"", "keep \\n as is")]
    public void Unquote_ResolvesEscapesInOnePass(string input, string expected)
    {
        _ = ScriptQuoting.Unquote(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("\"a\\\"b\"", "a\\\"b")]
    [InlineData("'x'", "x")]
    [InlineData("x", "x")]
    [InlineData("\"x'", "\"x'")]
    public void StripQuotes_RemovesOnlyTheSurroundingPair(string input, string expected)
    {
        _ = ScriptQuoting.StripQuotes(input).Should().Be(expected);
    }
}
