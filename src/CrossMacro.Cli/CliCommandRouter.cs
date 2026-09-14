
namespace CrossMacro.Cli;

#pragma warning disable S1118, MA0036 // Keep the public constructible type for compatibility; the API is static by design.
public sealed class CliCommandRouter
#pragma warning restore S1118, MA0036
{
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

        if (IsHelpToken(first))
        {
            return CliParseResult.Help();
        }

        if (IsVersionToken(first))
        {
            return CliParseResult.Version();
        }

        if (!IsCliCommandToken(first))
        {
            if (IsStandaloneCliOptionToken(first))
            {
                return CliParseResult.Error(
                    $"Option {first} requires a command.",
                    ["See crossmacro --help for usage information."],
                    prefersJsonOutput: string.Equals(first, "--json", StringComparison.OrdinalIgnoreCase),
                    showTopLevelUsageInTextMode: true);
            }

            if (ShouldTreatAsGuiStartup(first))
            {
                return CliParseResult.Gui();
            }

            if (LooksLikeOptionToken(first))
            {
                return CliParseResult.Error(
                    $"Unknown option: {first}",
                    ["See crossmacro --help for usage information."],
                    prefersJsonOutput: string.Equals(first, "--json", StringComparison.OrdinalIgnoreCase)
                        || CliParseHelpers.HasJsonOption(arguments, 1),
                    showTopLevelUsageInTextMode: true);
            }

            return CliParseResult.Error(
                $"Unknown command: {first}",
                ["See crossmacro --help for usage information."],
                prefersJsonOutput: CliParseHelpers.HasJsonOption(arguments, 1),
                showTopLevelUsageInTextMode: true);
        }

        if (TryGetRootCommand(first, out var command) && command is not null)
        {
            return command.ParseCommand(arguments);
        }

        return CliParseResult.Error(
            $"Unknown command: {first}",
            ["See crossmacro --help for usage information."],
            prefersJsonOutput: CliParseHelpers.HasJsonOption(arguments, 1),
            showTopLevelUsageInTextMode: true);
    }

    public static string GetUsage(string? topic = null) => CliHelpCatalog.GetUsage(topic);

    private static bool IsCliCommandToken(string firstToken)
    {
        return CliCommandCatalog.RootCommandLookup.ContainsKey(firstToken);
    }

    private static bool TryGetRootCommand(
        string token,
        out CliCommandCatalog.RootCommandDescriptor? command)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            command = null;
            return false;
        }

        return CliCommandCatalog.RootCommandLookup.TryGetValue(token, out command);
    }

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

    private static bool LooksLikeOptionToken(string token)
    {
        return token.StartsWith('-');
    }

    private static bool IsStandaloneCliOptionToken(string token)
    {
        return CliCommandCatalog.StandaloneCliOptionTokens.Contains(token);
    }

    private static bool IsHelpToken(string token)
    {
        return CliParseHelpers.IsHelpToken(token);
    }

    private static bool IsVersionToken(string token)
    {
        return CliParseHelpers.IsVersionToken(token);
    }
}
