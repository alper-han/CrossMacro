namespace CrossMacro.Cli.Tests;

public sealed class CliValueTokensTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("On", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("NO", false)]
    [InlineData("off", false)]
    public void TryParseBool_AcceptsEverySpelling(string token, bool expected)
    {
        Assert.True(CliValueTokens.TryParseBool(token, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("maybe")]
    [InlineData("2")]
    public void TryParseBool_RejectsEverythingElse(string? token)
    {
        Assert.False(CliValueTokens.TryParseBool(token, out _));
    }

    [Theory]
    [InlineData("verbose", "Verbose")]
    [InlineData("DEBUG", "Debug")]
    [InlineData("Information", "Information")]
    [InlineData("warning", "Warning")]
    [InlineData("error", "Error")]
    [InlineData("fatal", "Fatal")]
    public void TryNormalizeLogLevel_ReturnsCanonicalName(string token, string expected)
    {
        Assert.True(CliValueTokens.TryNormalizeLogLevel(token, out var level));
        Assert.Equal(expected, level);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("info")]
    public void TryNormalizeLogLevel_RejectsUnknownLevels(string? token)
    {
        Assert.False(CliValueTokens.TryNormalizeLogLevel(token, out _));
    }

    [Theory]
    [InlineData("precision", MotionPlaybackMode.Precision)]
    [InlineData("Precision", MotionPlaybackMode.Precision)]
    [InlineData("strict-speed", MotionPlaybackMode.StrictSpeed)]
    [InlineData("strictspeed", MotionPlaybackMode.StrictSpeed)]
    [InlineData("strict", MotionPlaybackMode.StrictSpeed)]
    [InlineData(" STRICT ", MotionPlaybackMode.StrictSpeed)]
    public void TryParseMotionMode_AcceptsEverySpelling(string token, MotionPlaybackMode expected)
    {
        Assert.True(CliValueTokens.TryParseMotionMode(token, out var mode));
        Assert.Equal(expected, mode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fast")]
    public void TryParseMotionMode_RejectsUnknownModes(string? token)
    {
        Assert.False(CliValueTokens.TryParseMotionMode(token, out _));
    }

    [Theory]
    [InlineData("auto", RecordCoordinateMode.Auto)]
    [InlineData("absolute", RecordCoordinateMode.Absolute)]
    [InlineData("ABS", RecordCoordinateMode.Absolute)]
    [InlineData("relative", RecordCoordinateMode.Relative)]
    [InlineData("rel", RecordCoordinateMode.Relative)]
    public void TryParseRecordCoordinateMode_AcceptsEverySpelling(string token, RecordCoordinateMode expected)
    {
        Assert.True(CliValueTokens.TryParseRecordCoordinateMode(token, out var mode));
        Assert.Equal(expected, mode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("mixed")]
    public void TryParseRecordCoordinateMode_RejectsUnknownModes(string? token)
    {
        Assert.False(CliValueTokens.TryParseRecordCoordinateMode(token, out _));
    }

    [Fact]
    public void ContractCatalog_UsesTheSharedTokenLists()
    {
        var play = CliCommandContractCatalog.RootCommands.Single(command => command.CommandToken is "play");
        var record = CliCommandContractCatalog.RootCommands.Single(command => command.CommandToken is "record");

        Assert.Equal(CliValueTokens.MotionModes, play.Options.Single(option => option.Token is "--motion-mode").AllowedValues);
        Assert.Equal(CliValueTokens.RecordCoordinateModes, record.Options.Single(option => option.Token is "--mode").AllowedValues);
        Assert.Equal(CliValueTokens.LogLevels, play.Options.Single(option => option.Token is "--log-level").AllowedValues);
    }

    [Fact]
    public void Parse_Play_AcceptsEveryMotionModeSpellingThatSettingsAccepts()
    {
        foreach (var token in CliValueTokens.MotionModes)
        {
            var result = CliCommandRouter.Parse(["play", "/tmp/test.macro", "--motion-mode", token]);

            Assert.True(result.IsSuccess, token);
        }
    }
}
