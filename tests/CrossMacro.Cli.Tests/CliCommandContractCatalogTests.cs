namespace CrossMacro.Cli.Tests;

public sealed class CliCommandContractCatalogTests
{
    [Fact]
    public void PublicContractCatalog_ShouldExposeTheCanonicalParserCatalog()
    {
        var expected = CliCommandCatalog.RootCommands.ToArray();
        var actual = CliCommandContractCatalog.RootCommands;

        Assert.Equal(expected.Length, actual.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].CommandToken, actual[index].CommandToken);
            Assert.Equal(expected[index].Aliases, actual[index].Aliases);
            Assert.All(actual[index].Subcommands, subcommand =>
            {
                var result = CliCommandRouter.Parse([expected[index].CommandToken, subcommand, "--help"]);
                Assert.True(result.ShowHelp, $"{expected[index].CommandToken} {subcommand}");
            });
            Assert.Equal(
                actual[index].Options.Select(static option => option.Token),
                actual[index].Options.Select(static option => option.Token).Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void PublicContractCatalog_ShouldHaveUniqueTokensAndAliases()
    {
        var allTokens = CliCommandContractCatalog.RootCommands
            .SelectMany(static command => new[] { command.CommandToken }.Concat(command.Aliases))
            .ToArray();

        Assert.Equal(allTokens.Length, allTokens.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var command in CliCommandContractCatalog.RootCommands)
        {
            Assert.Equal(
                command.Options.Count,
                command.Options.Select(static option => option.Token).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(command.Options, option =>
            {
                Assert.Contains(option.Token, CliCommandCatalog.StandaloneCliOptionTokens, StringComparer.OrdinalIgnoreCase);
                if (option.ValueKind is CliOptionValueKind.Enum)
                {
                    Assert.NotEmpty(option.AllowedValues);
                }
            });
        }
    }

    [Fact]
    public void PublicContractCatalog_ShouldMarkValueTakingBooleanOptions()
    {
        var options = CliCommandContractCatalog.RootCommands
            .SelectMany(static command => command.Options)
            .Where(static option => option.Token is "--enabled" or "--mouse" or "--keyboard")
            .ToArray();

        Assert.NotEmpty(options);
        Assert.All(options, option =>
        {
            Assert.Equal(CliOptionValueKind.Boolean, option.ValueKind);
            Assert.True(option.RequiresValue, option.Token);
        });
    }
}
