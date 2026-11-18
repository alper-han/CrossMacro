
namespace CrossMacro.Cli.Parsing;

internal static class MacroCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2)
        {
            return CliParseResult.Error("Missing macro subcommand. Expected: validate or info");
        }

        if (CliParseHelpers.IsHelpToken(args[1]))
        {
            return CliParseResult.Help("macro");
        }

        var subcommand = args[1];
        if (!string.Equals(subcommand, "validate", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(subcommand, "info", StringComparison.OrdinalIgnoreCase))
        {
            return CliParseResult.Error(
                $"Unknown macro subcommand: {subcommand}",
                prefersJsonOutput: string.Equals(subcommand, "--json", StringComparison.OrdinalIgnoreCase)
                    || CliParseHelpers.HasJsonOption(args, 2));
        }

        if (args.Length >= 3 && CliParseHelpers.IsHelpToken(args[2]))
        {
            return CliParseResult.Help($"macro.{subcommand.ToLowerInvariant()}");
        }

        if (args.Length < 3)
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                2,
                $"Missing <macro-file> argument for macro {subcommand}",
                MacroUsage(subcommand));
        }

        var macroFilePath = args[2];
        if (CliParseHelpers.IsHelpToken(macroFilePath))
        {
            return CliParseResult.Help($"macro.{subcommand.ToLowerInvariant()}");
        }

        if (string.IsNullOrWhiteSpace(macroFilePath)
            || (CliParseHelpers.LooksLikeOptionToken(macroFilePath)
                && string.Equals(macroFilePath, "--json", StringComparison.OrdinalIgnoreCase)))
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                2,
                $"Missing <macro-file> argument for macro {subcommand}",
                MacroUsage(subcommand));
        }

        var jsonOutput = false;
        string? logLevel = null;
        for (var i = 3; i < args.Length; i++)
        {
            var token = args[i];
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, $"macro.{subcommand.ToLowerInvariant()}", ref jsonOutput, ref logLevel, out var commonResult))
            {
                if (commonResult is not null)
                {
                    return commonResult;
                }

                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for macro {subcommand}: {token}", jsonOutput);
        }

        return string.Equals(subcommand, "validate", StringComparison.OrdinalIgnoreCase)
            ? CliParseResult.Success(new MacroValidateOptions(macroFilePath, jsonOutput, logLevel))
            : CliParseResult.Success(new MacroInfoOptions(macroFilePath, jsonOutput, logLevel));
    }

    private static string MacroUsage(string subcommand) =>
        string.Equals(subcommand, "validate", StringComparison.OrdinalIgnoreCase) ? CliUsage.MacroValidate : CliUsage.MacroInfo;
}
