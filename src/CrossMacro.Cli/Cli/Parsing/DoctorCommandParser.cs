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

            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "doctor", ref jsonOutput, ref logLevel, out var commonResult))
            {
                if (commonResult != null)
                {
                    return commonResult;
                }

                continue;
            }

            if (string.Equals(token, "--verbose", StringComparison.OrdinalIgnoreCase))
            {
                verbose = true;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for doctor: {token}", jsonOutput);
        }

        return CliParseResult.Success(new DoctorCliOptions(Verbose: verbose, JsonOutput: jsonOutput, LogLevel: logLevel));
    }
}
