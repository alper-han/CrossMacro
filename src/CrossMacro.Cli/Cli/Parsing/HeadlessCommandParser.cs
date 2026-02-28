using System;

namespace CrossMacro.Cli;

internal static class HeadlessCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;

        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];
            if (string.Equals(token, "--json", StringComparison.OrdinalIgnoreCase))
            {
                jsonOutput = true;
                continue;
            }

            if (string.Equals(token, "--log-level", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadLogLevel(args, ref i, out logLevel, out var error))
                {
                    return CliParseResult.Error(error);
                }
                continue;
            }

            if (CliParseHelpers.IsHelpToken(token))
            {
                return CliParseResult.Help("headless");
            }

            return CliParseResult.Error($"Unknown option for headless: {token}");
        }

        return CliParseResult.Success(new HeadlessCliOptions(jsonOutput, logLevel));
    }
}
