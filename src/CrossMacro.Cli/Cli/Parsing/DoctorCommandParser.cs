using System;

namespace CrossMacro.Cli;

internal static class DoctorCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        var verbose = false;
        var jsonOutput = false;
        string? logLevel = null;

        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];
            if (string.Equals(token, "--verbose", StringComparison.OrdinalIgnoreCase))
            {
                verbose = true;
                continue;
            }

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
                return CliParseResult.Help("doctor");
            }

            return CliParseResult.Error($"Unknown option for doctor: {token}");
        }

        return CliParseResult.Success(new DoctorCliOptions(Verbose: verbose, JsonOutput: jsonOutput, LogLevel: logLevel));
    }
}
