using System;

namespace CrossMacro.Cli;

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
            return CliParseResult.Error($"Unknown macro subcommand: {subcommand}");
        }

        if (args.Length >= 3 && CliParseHelpers.IsHelpToken(args[2]))
        {
            return CliParseResult.Help($"macro.{subcommand.ToLowerInvariant()}");
        }

        if (args.Length < 3)
        {
            return CliParseResult.Error($"Missing <macro-file> argument for macro {subcommand}");
        }

        var macroFilePath = args[2];
        if (string.IsNullOrWhiteSpace(macroFilePath))
        {
            return CliParseResult.Error("Macro file path cannot be empty");
        }

        var jsonOutput = false;
        string? logLevel = null;
        for (var i = 3; i < args.Length; i++)
        {
            var token = args[i];
            if (string.Equals(token, "--json", StringComparison.OrdinalIgnoreCase))
            {
                jsonOutput = true;
                continue;
            }

            if (string.Equals(token, "--log-level", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadLogLevel(args, ref i, out logLevel, out var logLevelError))
                {
                    return CliParseResult.Error(logLevelError);
                }
                continue;
            }

            if (CliParseHelpers.IsHelpToken(token))
            {
                return CliParseResult.Help($"macro.{subcommand.ToLowerInvariant()}");
            }

            return CliParseResult.Error($"Unknown option for macro {subcommand}: {token}");
        }

        return string.Equals(subcommand, "validate", StringComparison.OrdinalIgnoreCase)
            ? CliParseResult.Success(new MacroValidateCliOptions(macroFilePath, jsonOutput, logLevel))
            : CliParseResult.Success(new MacroInfoCliOptions(macroFilePath, jsonOutput, logLevel));
    }
}
