
namespace CrossMacro.Cli;

internal static class ClipboardCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2 || CliParseHelpers.IsHelpToken(args[1]))
        {
            return args.Length >= 2 && CliParseHelpers.IsHelpToken(args[1])
                ? CliParseResult.Help("clipboard")
                : CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                    args,
                    1,
                    "clipboard requires get, set, or clear.",
                    "crossmacro clipboard get [--json] [--log-level <level>]",
                    "crossmacro clipboard set <text> [--json] [--log-level <level>]",
                    "crossmacro clipboard set --file <path> [--json] [--log-level <level>]",
                    "crossmacro clipboard clear [--json] [--log-level <level>]");
        }

        return args[1].ToLowerInvariant() switch
        {
            "get" => ParseGet(args),
            "set" => ParseSet(args),
            "clear" => ParseClear(args),
            _ => CliParseResult.Error($"Unknown clipboard subcommand: {args[1]}", prefersJsonOutput: CliParseHelpers.HasJsonOption(args, 2)),
        };
    }

    private static CliParseResult ParseGet(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "clipboard.get", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for clipboard get: {args[i]}", jsonOutput);
        }

        return CliParseResult.Success(new ClipboardCliOptions(ClipboardCliAction.Get, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseClear(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "clipboard.clear", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for clipboard clear: {args[i]}", jsonOutput);
        }

        return CliParseResult.Success(new ClipboardCliOptions(ClipboardCliAction.Clear, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseSet(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        string? text = null;
        string? filePath = null;

        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "clipboard.set", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            if (string.Equals(args[i], "--file", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadNonEmptyString(args, ref i, out filePath, out var fileError))
                {
                    return CliParseHelpers.Error(fileError, jsonOutput);
                }

                continue;
            }

            if (args[i].StartsWith("-", StringComparison.Ordinal))
            {
                return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for clipboard set: {args[i]}", jsonOutput);
            }

            if (text is not null)
            {
                return CliParseHelpers.Error("clipboard set accepts one <text> operand.", jsonOutput);
            }

            text = args[i];
        }

        if (text is not null && filePath is not null)
        {
            return CliParseHelpers.Error("clipboard set accepts either <text> or --file <path>, not both.", jsonOutput);
        }

        if (text is null && filePath is null)
        {
            return CliParseHelpers.MissingRequiredOperands(
                "clipboard set requires <text> or --file <path>.",
                jsonOutput,
                "crossmacro clipboard set <text> [--json] [--log-level <level>]",
                "crossmacro clipboard set --file <path> [--json] [--log-level <level>]");
        }

        return CliParseResult.Success(new ClipboardCliOptions(ClipboardCliAction.Set, text, filePath, jsonOutput, logLevel));
    }
}
