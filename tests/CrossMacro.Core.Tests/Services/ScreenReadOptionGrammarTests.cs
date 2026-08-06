namespace CrossMacro.Core.Tests.Services;

public sealed class ScreenReadOptionGrammarTests
{
    [Theory]
    [InlineData("timeout", ScreenReadOptionKind.Timeout)]
    [InlineData("poll", ScreenReadOptionKind.Poll)]
    [InlineData("matchmode", ScreenReadOptionKind.MatchMode)]
    [InlineData("scaleaware", ScreenReadOptionKind.ScaleAware)]
    [InlineData("button", ScreenReadOptionKind.Button)]
    public void GetScriptOptionKind_MapsCanonicalScriptTokens(string token, ScreenReadOptionKind expected)
    {
        Assert.Equal(expected, ScreenReadOptionGrammar.GetScriptOptionKind(token));
    }

    [Theory]
    [InlineData("--timeout-ms", ScreenReadOptionKind.Timeout)]
    [InlineData("--poll-ms", ScreenReadOptionKind.PollInterval)]
    [InlineData("--matchmode", ScreenReadOptionKind.MatchMode)]
    [InlineData("--scale-aware", ScreenReadOptionKind.ScaleAware)]
    [InlineData("--button", ScreenReadOptionKind.Button)]
    public void GetCliOptionKind_MapsCanonicalCliTokens(string token, ScreenReadOptionKind expected)
    {
        Assert.Equal(expected, ScreenReadOptionGrammar.GetCliOptionKind(token));
    }

    [Fact]
    public void Grammar_DoesNotTreatCliOnlyPollIntervalAsScriptPoll()
    {
        Assert.Equal(ScreenReadOptionKind.Unknown, ScreenReadOptionGrammar.GetScriptOptionKind("poll-ms"));
        Assert.Equal(ScreenReadOptionKind.PollInterval, ScreenReadOptionGrammar.GetCliOptionKind("--poll-ms"));
    }
}
