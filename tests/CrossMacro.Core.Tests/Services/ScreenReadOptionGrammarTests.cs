namespace CrossMacro.Core.Tests.Services;

public sealed class ScreenReadOptionGrammarTests
{
    [Theory]
    [InlineData("timeout", ScreenReadOptionKind.Timeout)]
    [InlineData("matchmode", ScreenReadOptionKind.MatchMode)]
    [InlineData("button", ScreenReadOptionKind.Button)]
    public void GetScriptOptionKind_MapsCanonicalScriptTokens(string token, ScreenReadOptionKind expected)
    {
        Assert.Equal(expected, ScreenReadOptionGrammar.GetScriptOptionKind(token));
    }

    [Theory]
    [InlineData("--timeout-ms", ScreenReadOptionKind.Timeout)]
    [InlineData("--matchmode", ScreenReadOptionKind.MatchMode)]
    [InlineData("--button", ScreenReadOptionKind.Button)]
    public void GetCliOptionKind_MapsCanonicalCliTokens(string token, ScreenReadOptionKind expected)
    {
        Assert.Equal(expected, ScreenReadOptionGrammar.GetCliOptionKind(token));
    }

    [Fact]
    public void Grammar_RejectsRetiredPollingTokens()
    {
        Assert.Equal(ScreenReadOptionKind.Unknown, ScreenReadOptionGrammar.GetScriptOptionKind("poll-ms"));
        Assert.Equal(ScreenReadOptionKind.Unknown, ScreenReadOptionGrammar.GetScriptOptionKind("poll"));
        Assert.Equal(ScreenReadOptionKind.Unknown, ScreenReadOptionGrammar.GetCliOptionKind("--poll"));
        Assert.Equal(ScreenReadOptionKind.Unknown, ScreenReadOptionGrammar.GetCliOptionKind("--poll-ms"));
    }

    [Theory]
    [InlineData("downsample")]
    [InlineData("scaleaware")]
    public void GetScriptOptionKind_LegacyTuningTokensAreUnknown(string token)
    {
        Assert.Equal(ScreenReadOptionKind.Unknown, ScreenReadOptionGrammar.GetScriptOptionKind(token));
    }

    [Theory]
    [InlineData("--downsample")]
    [InlineData("--scale-aware")]
    public void GetCliOptionKind_LegacyTuningTokensAreUnknown(string token)
    {
        Assert.Equal(ScreenReadOptionKind.Unknown, ScreenReadOptionGrammar.GetCliOptionKind(token));
    }
}
