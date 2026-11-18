namespace CrossMacro.Cli.Tests;

public sealed class CommandValueTokensGrammarTests
{
    [Fact]
    public void ContractCatalog_UsesTheSharedTokenLists()
    {
        var play = CliCommandContractCatalog.RootCommands.Single(command => command.CommandToken is "play");
        var record = CliCommandContractCatalog.RootCommands.Single(command => command.CommandToken is "record");

        Assert.Equal(CommandValueTokens.MotionModes, play.Options.Single(option => option.Token is "--motion-mode").AllowedValues);
        Assert.Equal(CommandValueTokens.RecordCoordinateModes, record.Options.Single(option => option.Token is "--mode").AllowedValues);
        Assert.Equal(CommandValueTokens.LogLevels, play.Options.Single(option => option.Token is "--log-level").AllowedValues);
    }

    [Fact]
    public void Parse_Play_AcceptsEveryMotionModeSpellingThatSettingsAccepts()
    {
        foreach (var token in CommandValueTokens.MotionModes)
        {
            var result = CliCommandRouter.Parse(["play", "/tmp/test.macro", "--motion-mode", token]);

            Assert.True(result.IsSuccess, token);
        }
    }
}
