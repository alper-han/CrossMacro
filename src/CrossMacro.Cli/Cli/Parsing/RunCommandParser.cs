using System;
using System.Collections.Generic;
using System.Globalization;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

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

        if (speed < PlaybackOptions.MinSpeedMultiplier || speed > PlaybackOptions.MaxSpeedMultiplier)
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
                error = $"Invalid inline step syntax for {token}. Expected: {token} <button> or {token} {RunScriptSyntax.CurrentPositionToken} <button>";
                return false;
            }

            if (RunScriptSyntax.IsCurrentPositionToken(args[index + 1]))
            {
                if (index + 2 >= args.Length)
                {
                    error = $"Invalid inline step syntax for {token}. Expected: {token} {RunScriptSyntax.CurrentPositionToken} <button>";
                    return false;
                }

                step = $"{token} {RunScriptSyntax.CurrentPositionToken} {args[index + 2]}";
                index += 2;
                return true;
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

        if (string.Equals(token, "inc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "dec", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                error = $"Invalid inline step syntax for {token}. Expected: {token} <name> [amount]";
                return false;
            }

            if (index + 2 < args.Length
                && (int.TryParse(args[index + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                    || IsVariableReferenceToken(args[index + 2])))
            {
                step = $"{token} {args[index + 1]} {args[index + 2]}";
                index += 2;
                return true;
            }

            step = $"{token} {args[index + 1]}";
            index += 1;
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

        if (string.Equals(token, "if", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "while", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 4 >= args.Length || !string.Equals(args[index + 4], "{", StringComparison.Ordinal))
            {
                error = $"Invalid inline step syntax for {token}. Expected: {token} <left> <op> <right> {{";
                return false;
            }

            step = $"{token} {args[index + 1]} {args[index + 2]} {args[index + 3]} {{";
            index += 4;
            return true;
        }

        if (string.Equals(token, "else", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length || !string.Equals(args[index + 1], "{", StringComparison.Ordinal))
            {
                error = "Invalid inline step syntax for else. Expected: else {";
                return false;
            }

            step = "else {";
            index += 1;
            return true;
        }

        if (string.Equals(token, "for", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 5 >= args.Length)
            {
                error = "Invalid inline step syntax for for. Expected: for <var> from <start> to <end> [step <n>] {";
                return false;
            }

            if (!string.Equals(args[index + 2], "from", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(args[index + 4], "to", StringComparison.OrdinalIgnoreCase))
            {
                error = "Invalid inline step syntax for for. Expected: for <var> from <start> to <end> [step <n>] {";
                return false;
            }

            if (index + 6 < args.Length && string.Equals(args[index + 6], "{", StringComparison.Ordinal))
            {
                step = $"for {args[index + 1]} from {args[index + 3]} to {args[index + 5]} {{";
                index += 6;
                return true;
            }

            if (index + 8 < args.Length
                && string.Equals(args[index + 6], "step", StringComparison.OrdinalIgnoreCase)
                && string.Equals(args[index + 8], "{", StringComparison.Ordinal))
            {
                step = $"for {args[index + 1]} from {args[index + 3]} to {args[index + 5]} step {args[index + 7]} {{";
                index += 8;
                return true;
            }

            error = "Invalid inline step syntax for for. Expected: for <var> from <start> to <end> [step <n>] {";
            return false;
        }

        if (RunScriptSyntax.IsBreakCommand(token))
        {
            step = RunScriptSyntax.BreakCommand;
            return true;
        }

        if (RunScriptSyntax.IsContinueCommand(token))
        {
            step = RunScriptSyntax.ContinueCommand;
            return true;
        }

        if (RunScriptSyntax.IsBlockEndToken(token))
        {
            step = RunScriptSyntax.BlockEndToken;
            return true;
        }

        error = $"Unknown inline run step command: {token}";
        return false;
    }

    private static bool IsVariableReferenceToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith('$'))
        {
            return false;
        }

        var name = token[1..];
        if (name.Length == 0)
        {
            return false;
        }

        if (!(name[0] == '_' || char.IsLetter(name[0])))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (!(name[i] == '_' || char.IsLetterOrDigit(name[i])))
            {
                return false;
            }
        }

        return true;
    }
}
