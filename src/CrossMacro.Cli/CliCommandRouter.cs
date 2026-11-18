
namespace CrossMacro.Cli;

public static class CliCommandRouter
{
    private const string UsageHint = "See crossmacro --help for usage information.";

    public static CliParseResult Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return Parse(args.AsMemory());
    }

    public static CliParseResult Parse(ReadOnlyMemory<string> args)
    {
        if (args.Length is 0)
        {
            return CliParseResult.Gui();
        }

        var arguments = args.ToArray();
        var first = arguments[0];

        if (CliParseHelpers.IsHelpToken(first))
        {
            return CliParseResult.Help();
        }

        if (CliParseHelpers.IsVersionToken(first))
        {
            return CliParseResult.Version();
        }

        if (CliCommandCatalog.RootCommandLookup.TryGetValue(first, out var command))
        {
            return command.ParseCommand(arguments);
        }

        if (CliCommandCatalog.StandaloneCliOptionTokens.Contains(first))
        {
            return CliParseResult.Error(
                $"Option {first} requires a command.",
                [UsageHint],
                prefersJsonOutput: string.Equals(first, "--json", StringComparison.OrdinalIgnoreCase),
                showTopLevelUsageInTextMode: true);
        }

        if (ShouldTreatAsGuiStartup(first))
        {
            return CliParseResult.Gui();
        }

        if (first.StartsWith('-'))
        {
            return CliParseResult.Error(
                $"Unknown option: {first}",
                [UsageHint],
                prefersJsonOutput: string.Equals(first, "--json", StringComparison.OrdinalIgnoreCase)
                    || CliParseHelpers.HasJsonOption(arguments, 1),
                showTopLevelUsageInTextMode: true);
        }

        return CliParseResult.Error(
            $"Unknown command: {first}",
            [UsageHint],
            prefersJsonOutput: CliParseHelpers.HasJsonOption(arguments, 1),
            showTopLevelUsageInTextMode: true);
    }

    public static string GetUsage(string? topic = null) => CliHelpCatalog.GetUsage(topic);

    private static bool ShouldTreatAsGuiStartup(string firstToken)
    {
        if (string.IsNullOrWhiteSpace(firstToken))
        {
            return true;
        }

        if (CliCommandCatalog.KnownGuiStartupOptionTokens.Contains(firstToken))
        {
            return true;
        }

        if (firstToken.StartsWith("-psn_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return CliCommandCatalog.KnownGuiStartupOptionPrefixes.Any(prefix =>
            string.Equals(firstToken, prefix, StringComparison.OrdinalIgnoreCase)
            || firstToken.StartsWith($"{prefix}=", StringComparison.OrdinalIgnoreCase));
    }
}
