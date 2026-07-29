
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
}
