namespace CrossMacro.Cli.Parsing;

internal static class McpCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        string? logLevel = null;
        var restricted = false;

        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];
            if (string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase))
            {
                return CliParseResult.Help("mcp");
            }

            if (string.Equals(token, "--log-level", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadLogLevel(args, ref i, out logLevel, out var error))
                {
                    return CliParseResult.Error(error);
                }

                continue;
            }

            if (string.Equals(token, "--restricted", StringComparison.OrdinalIgnoreCase))
            {
                restricted = true;
                continue;
            }

            return CliParseResult.Error($"Unknown option for mcp: {token}");
        }

        return CliParseResult.Success(new McpCliOptions(logLevel, restricted));
    }
}
