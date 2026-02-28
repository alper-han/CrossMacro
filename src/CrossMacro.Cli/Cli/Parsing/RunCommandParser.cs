using System;
using System.Collections.Generic;
using System.Globalization;

namespace CrossMacro.Cli;

internal static class RunCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        var steps = new List<string>();
        string? stepFilePath = null;
        var speed = 1.0;
        var countdown = 0;
        var timeout = 0;
        var dryRun = false;
        var jsonOutput = false;
        string? logLevel = null;

        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];

            if (CliParseHelpers.IsHelpToken(token))
            {
                return CliParseResult.Help("run");
            }

            if (string.Equals(token, "--step", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadNonEmptyString(args, ref i, out var step, out var stepError))
                {
                    return CliParseResult.Error(stepError);
                }

                steps.Add(step);
                continue;
            }

            if (string.Equals(token, "--file", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadNonEmptyString(args, ref i, out stepFilePath, out var fileError))
                {
                    return CliParseResult.Error(fileError);
                }

                continue;
            }

            if (string.Equals(token, "--speed", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadDouble(args, ref i, out speed, out var speedError))
                {
                    return CliParseResult.Error(speedError);
                }

                continue;
            }

            if (string.Equals(token, "--countdown", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out countdown, out var countdownError))
                {
                    return CliParseResult.Error(countdownError);
                }

                continue;
            }

            if (string.Equals(token, "--timeout", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out timeout, out var timeoutError))
                {
                    return CliParseResult.Error(timeoutError);
                }

                continue;
            }

            if (string.Equals(token, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
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

            if (!token.StartsWith("-", StringComparison.Ordinal))
            {
                if (!TryParseInlineRunStep(args, ref i, out var inlineStep, out var inlineError))
                {
                    return CliParseResult.Error(inlineError);
                }

                steps.Add(inlineStep);
                continue;
            }

            return CliParseResult.Error($"Unknown option for run: {token}");
        }

        if (steps.Count == 0 && string.IsNullOrWhiteSpace(stepFilePath))
        {
            return CliParseResult.Error("Usage: run --step <step> [--step <step> ...] [--file <steps-file>] [--speed <value>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]");
        }

        if (countdown < 0)
        {
            return CliParseResult.Error("--countdown must be >= 0");
        }

        if (timeout < 0)
        {
            return CliParseResult.Error("--timeout must be >= 0");
        }

        if (speed < 0.1 || speed > 10.0)
        {
            return CliParseResult.Error("--speed must be between 0.1 and 10.");
        }

        return CliParseResult.Success(new RunCliOptions(
            Steps: steps,
            StepFilePath: stepFilePath,
            SpeedMultiplier: speed,
            CountdownSeconds: countdown,
            TimeoutSeconds: timeout,
            DryRun: dryRun,
            JsonOutput: jsonOutput,
            LogLevel: logLevel));
    }

    private static bool TryParseInlineRunStep(string[] args, ref int index, out string step, out string error)
    {
        step = string.Empty;
        error = string.Empty;

        var token = args[index];

        if (string.Equals(token, "move", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 3 >= args.Length)
            {
                error = "Invalid inline step syntax for move. Expected: move abs <x> <y> | move rel <dx> <dy>";
                return false;
            }

            step = $"move {args[index + 1]} {args[index + 2]} {args[index + 3]}";
            index += 3;
            return true;
        }

        if (string.Equals(token, "down", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "up", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "click", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                error = $"Invalid inline step syntax for {token}. Expected: {token} <button>";
                return false;
            }

            step = $"{token} {args[index + 1]}";
            index += 1;
            return true;
        }

        if (string.Equals(token, "scroll", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                error = "Invalid inline step syntax for scroll. Expected: scroll <up|down|left|right> [count]";
                return false;
            }

            if (index + 2 < args.Length && int.TryParse(args[index + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                step = $"scroll {args[index + 1]} {args[index + 2]}";
                index += 2;
                return true;
            }

            step = $"scroll {args[index + 1]}";
            index += 1;
            return true;
        }

        if (string.Equals(token, "key", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 2 >= args.Length)
            {
                error = "Invalid inline step syntax for key. Expected: key down <key> | key up <key>";
                return false;
            }

            step = $"key {args[index + 1]} {args[index + 2]}";
            index += 2;
            return true;
        }

        if (string.Equals(token, "delay", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                error = "Invalid inline step syntax for delay. Expected: delay <ms> or delay random <min> <max>";
                return false;
            }

            if (string.Equals(args[index + 1], "random", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 3 < args.Length
                    && int.TryParse(args[index + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                    && int.TryParse(args[index + 3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    step = $"delay random {args[index + 2]} {args[index + 3]}";
                    index += 3;
                    return true;
                }

                if (index + 2 < args.Length && args[index + 2].Contains("..", StringComparison.Ordinal))
                {
                    step = $"delay random {args[index + 2]}";
                    index += 2;
                    return true;
                }

                error = "Invalid inline step syntax for delay. Expected: delay random <min> <max> or delay random <min>..<max>";
                return false;
            }

            step = $"delay {args[index + 1]}";
            index += 1;
            return true;
        }

        if (string.Equals(token, "tap", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "type", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                error = $"Invalid inline step syntax for {token}. Expected: {token} <value>";
                return false;
            }

            step = $"{token} {args[index + 1]}";
            index += 1;
            return true;
        }

        if (string.Equals(token, "set", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                error = "Invalid inline step syntax for set. Expected: set <name> <value> or set <name>=<value>";
                return false;
            }

            if (args[index + 1].Contains('='))
            {
                step = $"set {args[index + 1]}";
                index += 1;
                return true;
            }

            if (index + 2 >= args.Length)
            {
                error = "Invalid inline step syntax for set. Expected: set <name> <value> or set <name>=<value>";
                return false;
            }

            step = $"set {args[index + 1]} {args[index + 2]}";
            index += 2;
            return true;
        }

        if (string.Equals(token, "repeat", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 2 >= args.Length || !string.Equals(args[index + 2], "{", StringComparison.Ordinal))
            {
                error = "Invalid inline step syntax for repeat. Expected: repeat <count> {";
                return false;
            }

            step = $"repeat {args[index + 1]} {{";
            index += 2;
            return true;
        }

        if (string.Equals(token, "}", StringComparison.Ordinal))
        {
            step = "}";
            return true;
        }

        error = $"Unknown inline run step command: {token}";
        return false;
    }
}
