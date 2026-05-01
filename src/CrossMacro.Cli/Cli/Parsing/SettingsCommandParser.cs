using System;

namespace CrossMacro.Cli;

internal static class SettingsCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2)
        {
            return CliParseResult.Error("Missing settings subcommand. Expected: get or set");
        }

        if (CliParseHelpers.IsHelpToken(args[1]))
        {
            return CliParseResult.Help("settings");
        }

        var subcommand = args[1];
        if (string.Equals(subcommand, "get", StringComparison.OrdinalIgnoreCase))
        {
            return ParseGet(args);
        }

        if (string.Equals(subcommand, "set", StringComparison.OrdinalIgnoreCase))
        {
            return ParseSet(args);
        }

        return CliParseResult.Error(
            $"Unknown settings subcommand: {subcommand}",
            prefersJsonOutput: string.Equals(subcommand, "--json", StringComparison.OrdinalIgnoreCase)
                || CliParseHelpers.HasJsonOption(args, 2));
    }

    private static CliParseResult ParseGet(string[] args)
    {
        if (args.Length >= 3 && CliParseHelpers.IsHelpToken(args[2]))
        {
            return CliParseResult.Help("settings.get");
        }

        string? key = null;
        var jsonOutput = false;
        string? logLevel = null;

        for (var i = 2; i < args.Length; i++)
        {
            var token = args[i];
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "settings.get", ref jsonOutput, ref logLevel, out var commonResult))
            {
                if (commonResult != null)
                {
                    return commonResult;
                }

                continue;
            }

            if (key == null)
            {
                key = token;
                continue;
            }

            return CliParseHelpers.Error($"Unexpected argument for settings get: {token}", jsonOutput);
        }

        return CliParseResult.Success(new SettingsGetCliOptions(key, jsonOutput, logLevel));
    }

    private static CliParseResult ParseSet(string[] args)
    {
        if (args.Length >= 3 && CliParseHelpers.IsHelpToken(args[2]))
        {
            return CliParseResult.Help("settings.set");
        }

        if (args.Length < 4)
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                3,
                "settings set requires <key> and <value>.",
                "crossmacro settings set <key> <value> [--json] [--log-level <level>]");
        }

        var key = args[2];
        var value = args[3];
        if (string.IsNullOrWhiteSpace(key) || CliParseHelpers.LooksLikeOptionToken(key))
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                3,
                "settings set requires <key> and <value>.",
                "crossmacro settings set <key> <value> [--json] [--log-level <level>]");
        }

        if (string.IsNullOrWhiteSpace(value) || CliParseHelpers.LooksLikeLongOptionToken(value))
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                3,
                "settings set requires <key> and <value>.",
                "crossmacro settings set <key> <value> [--json] [--log-level <level>]");
        }

        var jsonOutput = false;
        string? logLevel = null;
        for (var i = 4; i < args.Length; i++)
        {
            var token = args[i];
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "settings.set", ref jsonOutput, ref logLevel, out var commonResult))
            {
                if (commonResult != null)
                {
                    return commonResult;
                }

                continue;
            }

            return CliParseHelpers.Error($"Unknown option for settings set: {token}", jsonOutput);
        }

        return CliParseResult.Success(new SettingsSetCliOptions(key, value, jsonOutput, logLevel));
    }
}
