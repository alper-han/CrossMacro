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

            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "headless", ref jsonOutput, ref logLevel, out var commonResult))
            {
                if (commonResult != null)
                {
                    return commonResult;
                }

                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for headless: {token}", jsonOutput);
        }

        return CliParseResult.Success(new HeadlessCliOptions(jsonOutput, logLevel));
    }
}
