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

        return CliParseResult.Error($"Unknown {commandName} subcommand: {args[1]}");
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
                return CliParseResult.Help($"{commandName}.list");
            }

            return CliParseResult.Error($"Unknown option for {commandName} list: {token}");
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
            return CliParseResult.Error($"Usage: {commandName} run <task-id> [--json] [--log-level <level>]");
        }

        var taskId = args[2];
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return CliParseResult.Error("Task id cannot be empty");
        }

        var jsonOutput = false;
        string? logLevel = null;
        for (var i = 3; i < args.Length; i++)
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
                return CliParseResult.Help($"{commandName}.run");
            }

            return CliParseResult.Error($"Unknown option for {commandName} run: {token}");
        }

        return CliParseResult.Success(createRunOptions(taskId, jsonOutput, logLevel));
    }
}
