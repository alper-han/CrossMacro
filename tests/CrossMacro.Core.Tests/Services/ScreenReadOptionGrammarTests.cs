namespace CrossMacro.Core.Tests.Services;

public sealed class ScreenReadOptionGrammarTests
{
    [Theory]
    [InlineData("region", ScreenReadOptionKind.Region)]
    [InlineData(" TOLERANCE ", ScreenReadOptionKind.Tolerance)]
    [InlineData("similarity", ScreenReadOptionKind.Similarity)]
    [InlineData("timeout", ScreenReadOptionKind.Timeout)]
    [InlineData("matchmode", ScreenReadOptionKind.MatchMode)]
    [InlineData("button", ScreenReadOptionKind.Button)]
    public void GetScriptOptionKind_MapsCanonicalScriptTokens(string token, ScreenReadOptionKind expected)
    {
        Assert.Equal(expected, ScreenReadOptionGrammar.GetScriptOptionKind(token));
    }

    [Theory]
    [InlineData("--region", ScreenReadOptionKind.Region)]
    [InlineData(" --TOLERANCE ", ScreenReadOptionKind.Tolerance)]
    [InlineData("--similarity", ScreenReadOptionKind.Similarity)]
    [InlineData("--timeout-ms", ScreenReadOptionKind.Timeout)]
    [InlineData("--matchmode", ScreenReadOptionKind.MatchMode)]
    [InlineData("--button", ScreenReadOptionKind.Button)]
    public void GetCliOptionKind_MapsCanonicalCliTokens(string token, ScreenReadOptionKind expected)
    {
        Assert.Equal(expected, ScreenReadOptionGrammar.GetCliOptionKind(token));
    }

    [Theory]
    [InlineData(ScreenReadOptionKind.Unknown, false, false, false, false)]
    [InlineData(ScreenReadOptionKind.Region, false, false, false, false)]
    [InlineData(ScreenReadOptionKind.Tolerance, false, false, false, true)]
    [InlineData(ScreenReadOptionKind.Similarity, true, true, true, false)]
    [InlineData(ScreenReadOptionKind.MatchMode, true, true, true, false)]
    [InlineData(ScreenReadOptionKind.Timeout, false, true, true, true)]
    [InlineData(ScreenReadOptionKind.Button, false, false, true, false)]
    public void OptionKindPredicates_ReturnExpectedCapabilityMatrix(
        ScreenReadOptionKind kind,
        bool expectedImageMatch,
        bool expectedImageSearch,
        bool expectedImageClick,
        bool expectedPixelSearch)
    {
        Assert.Equal(expectedImageMatch, ScreenReadOptionGrammar.IsImageMatchOption(kind));
        Assert.Equal(expectedImageSearch, ScreenReadOptionGrammar.IsImageSearchOption(kind));
        Assert.Equal(expectedImageClick, ScreenReadOptionGrammar.IsImageClickOption(kind));
        Assert.Equal(expectedPixelSearch, ScreenReadOptionGrammar.IsPixelSearchOption(kind));
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
