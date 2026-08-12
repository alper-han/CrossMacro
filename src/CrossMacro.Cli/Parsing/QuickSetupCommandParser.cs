namespace CrossMacro.Cli.Parsing;

internal static class QuickSetupCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;

        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];

            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "setup", ref jsonOutput, ref logLevel, out var commonResult))
            {
                if (commonResult is not null)
                {
                    return commonResult;
                }

                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for setup: {token}", jsonOutput);
        }

        return CliParseResult.Success(new QuickSetupCliOptions(JsonOutput: jsonOutput, LogLevel: logLevel));
    }
}
