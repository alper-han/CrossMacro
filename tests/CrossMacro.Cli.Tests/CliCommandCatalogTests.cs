namespace CrossMacro.Cli.Tests;

public sealed class CliCommandCatalogTests
{
    [Fact]
    public void RootCommands_HaveUniqueCanonicalTokensAndAliases()
    {
        var canonicalTokens = CliCommandCatalog.RootCommands
            .Select(command => command.CommandToken)
            .ToArray();
        var allTokens = CliCommandCatalog.RootCommands
            .SelectMany(command => new[] { command.CommandToken }.Concat(command.Aliases))
            .ToArray();

        Assert.Equal(canonicalTokens.Length, canonicalTokens.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(allTokens.Length, allTokens.Distinct(StringComparer.OrdinalIgnoreCase).Count());
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
