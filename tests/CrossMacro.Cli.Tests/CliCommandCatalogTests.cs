namespace CrossMacro.Cli.Tests;

public sealed class CliCommandCatalogTests
{
    [Fact]
    public void RootCommandLookup_ContainsEveryCommandTokenAndAlias()
    {
        Assert.Equal(
            CliCommandCatalog.RootCommands.Count + CliCommandCatalog.RootCommands.Sum(command => command.Aliases.Length),
            CliCommandCatalog.RootCommandLookup.Count);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("--headless")]
    public void RootAliases_RouteToHelpWithoutChangingTheCommandContract(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);

        var result = CliCommandRouter.Parse([alias, "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal(alias.Equals("--headless", StringComparison.OrdinalIgnoreCase) ? "headless" : "text-expansion", result.HelpTopic);
    }

    [Fact]
    public void McpCommand_RoutesToThePersistentMcpOptionsWithCanonicalLogLevel()
    {
        var result = CliCommandRouter.Parse(["mcp", "--log-level", "debug"]);

        var options = Assert.IsType<McpOptions>(result.Options);
        Assert.Equal("Debug", options.LogLevel);
    }

    [Theory]
    [InlineData("trace")]
    [InlineData("")]
    public void McpCommand_RejectsInvalidLogLevels(string value)
    {
        var result = CliCommandRouter.Parse(["mcp", "--log-level", value]);

        Assert.False(result.IsSuccess);
        Assert.Contains("log-level", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void McpCommand_RejectsMissingLogLevelValue()
    {
        var result = CliCommandRouter.Parse(["mcp", "--log-level"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Missing value", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void McpCommand_ShouldSupportRestrictedMode()
    {
        var result = CliCommandRouter.Parse(["mcp", "--restricted"]);

        var options = Assert.IsType<McpOptions>(result.Options);
        Assert.True(options.Restricted);
    }

    [Fact]
    public void RootCommands_AllExposeHelpThroughTheCatalog()
    {
        foreach (var command in CliCommandCatalog.RootCommands)
        {
            var result = CliCommandRouter.Parse([command.CommandToken, "--help"]);

            Assert.True(result.ShowHelp, command.CommandToken);
            Assert.Equal(command.CommandToken, result.HelpTopic);
        }
    }

    [Theory]
    [InlineData("--json")]
    [InlineData("--log-level")]
    [InlineData("--output")]
    [InlineData("--region")]
    public void StandaloneOptions_RequireACommand(string option)
    {
        var result = CliCommandRouter.Parse([option]);

        Assert.False(result.IsSuccess);
        Assert.Contains("requires a command", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
