using System;

namespace CrossMacro.Cli;

internal static class TaskCommandParser
{
    public static CliParseResult Parse(
        string[] args,
        string commandName,
        Func<bool, string?, CliCommandOptions> createListOptions,
        Func<string, bool, string?, CliCommandOptions> createRunOptions)
    {
        if (args.Length < 2)
        {
            return CliParseResult.Error($"Missing {commandName} subcommand. Expected: list or run");
        }

        if (CliParseHelpers.IsHelpToken(args[1]))
        {
            return CliParseResult.Help(commandName);
        }

        if (string.Equals(args[1], "list", StringComparison.OrdinalIgnoreCase))
        {
            return ParseList(args, commandName, createListOptions);
        }

        if (string.Equals(args[1], "run", StringComparison.OrdinalIgnoreCase))
        {
            return ParseRun(args, commandName, createRunOptions);
        }

        return CliParseResult.Error(
            $"Unknown {commandName} subcommand: {args[1]}",
            prefersJsonOutput: string.Equals(args[1], "--json", StringComparison.OrdinalIgnoreCase)
                || CliParseHelpers.HasJsonOption(args, 2));
    }

    private static CliParseResult ParseList(
        string[] args,
        string commandName,
        Func<bool, string?, CliCommandOptions> createListOptions)
    {
        var jsonOutput = false;
        string? logLevel = null;
        for (var i = 2; i < args.Length; i++)
        {
            var token = args[i];

            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, $"{commandName}.list", ref jsonOutput, ref logLevel, out var commonResult))
            {
                if (commonResult != null)
                {
                    return commonResult;
                }

                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for {commandName} list: {token}", jsonOutput);
        }

        return CliParseResult.Success(createListOptions(jsonOutput, logLevel));
    }

    private static CliParseResult ParseRun(
        string[] args,
        string commandName,
        Func<string, bool, string?, CliCommandOptions> createRunOptions)
    {
        if (args.Length >= 3 && CliParseHelpers.IsHelpToken(args[2]))
        {
            return CliParseResult.Help($"{commandName}.run");
        }

        if (args.Length < 3)
        {
            return CliParseHelpers.MissingRequiredOperands(
                $"{commandName} run requires <task-id>.",
                CliParseHelpers.HasJsonOption(args, 2),
                $"crossmacro {commandName} run <task-id> [--json] [--log-level <level>]");
        }

        var taskId = args[2];
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return CliParseHelpers.Error("Task id cannot be empty", CliParseHelpers.HasJsonOption(args, 2));
        }

        if (CliParseHelpers.LooksLikeOptionToken(taskId))
        {
            return CliParseHelpers.MissingRequiredOperands(
                $"{commandName} run requires <task-id>.",
                CliParseHelpers.HasJsonOption(args, 2),
                $"crossmacro {commandName} run <task-id> [--json] [--log-level <level>]");
        }

        var jsonOutput = false;
        string? logLevel = null;
        for (var i = 3; i < args.Length; i++)
        {
            var token = args[i];

            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, $"{commandName}.run", ref jsonOutput, ref logLevel, out var commonResult))
            {
                if (commonResult != null)
                {
                    return commonResult;
                }

                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for {commandName} run: {token}", jsonOutput);
        }

        return CliParseResult.Success(createRunOptions(taskId, jsonOutput, logLevel));
    }
}
