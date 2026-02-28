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

        return CliParseResult.Error($"Unknown settings subcommand: {subcommand}");
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
                return CliParseResult.Help("settings.get");
            }

            if (key == null)
            {
                key = token;
                continue;
            }

            return CliParseResult.Error($"Unexpected argument for settings get: {token}");
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
            return CliParseResult.Error("Usage: settings set <key> <value> [--json] [--log-level <level>]");
        }

        var key = args[2];
        var value = args[3];
        if (string.IsNullOrWhiteSpace(key))
        {
            return CliParseResult.Error("Settings key cannot be empty");
        }

        var jsonOutput = false;
        string? logLevel = null;
        for (var i = 4; i < args.Length; i++)
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
                return CliParseResult.Help("settings.set");
            }

            return CliParseResult.Error($"Unknown option for settings set: {token}");
        }

        return CliParseResult.Success(new SettingsSetCliOptions(key, value, jsonOutput, logLevel));
    }
}
