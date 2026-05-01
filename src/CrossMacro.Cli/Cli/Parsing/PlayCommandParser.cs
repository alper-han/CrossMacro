using System;

namespace CrossMacro.Cli;

internal static class PlayCommandParser
{
    private const int OptionsStartIndex = 2;

    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2)
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                1,
                "Missing <macro-file> argument for play",
                "crossmacro play <macro-file> [--speed <value>] [--loop] [--repeat <n>] [--repeat-delay-ms <ms>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]");
        }

        if (CliParseHelpers.IsHelpToken(args[1]))
        {
            return CliParseResult.Help("play");
        }

        var macroFilePath = args[1];
        if (CliParseHelpers.IsHelpToken(macroFilePath))
        {
            return CliParseResult.Help("play");
        }

        if (string.IsNullOrWhiteSpace(macroFilePath)
            || (CliParseHelpers.LooksLikeOptionToken(macroFilePath)
                && string.Equals(macroFilePath, "--json", StringComparison.OrdinalIgnoreCase)))
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                1,
                "Missing <macro-file> argument for play",
                "crossmacro play <macro-file> [--speed <value>] [--loop] [--repeat <n>] [--repeat-delay-ms <ms>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]");
        }

        var speed = 1.0;
        var loop = false;
        var repeat = 1;
        var repeatProvided = false;
        var repeatDelayMs = 0;
        var countdown = 0;
        var timeout = 0;
        var dryRun = false;
        var jsonOutput = false;
        string? logLevel = null;

        for (var i = 2; i < args.Length; i++)
        {
            var token = args[i];

            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "play", ref jsonOutput, ref logLevel, i + 1, out var commonResult))
            {
                if (commonResult != null)
                {
                    return commonResult;
                }

                continue;
            }

            if (string.Equals(token, "--loop", StringComparison.OrdinalIgnoreCase))
            {
                loop = true;
                continue;
            }

            if (string.Equals(token, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
                continue;
            }

            if (string.Equals(token, "--speed", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadDouble(args, ref i, out speed, out var error))
                {
                    return CliParseHelpers.Error(error, jsonOutput);
                }
                continue;
            }

            if (string.Equals(token, "--repeat", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out repeat, out var error))
                {
                    return CliParseHelpers.Error(error, jsonOutput);
                }
                repeatProvided = true;
                continue;
            }

            if (string.Equals(token, "--repeat-delay-ms", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out repeatDelayMs, out var error))
                {
                    return CliParseHelpers.Error(error, jsonOutput);
                }
                continue;
            }

            if (string.Equals(token, "--countdown", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out countdown, out var error))
                {
                    return CliParseHelpers.Error(error, jsonOutput);
                }
                continue;
            }

            if (string.Equals(token, "--timeout", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out timeout, out var error))
                {
                    return CliParseHelpers.Error(error, jsonOutput);
                }
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for play: {token}", jsonOutput);
        }

        if (repeat < 0)
        {
            return CliParseHelpers.Error("--repeat must be >= 0", jsonOutput);
        }

        if (repeatDelayMs < 0)
        {
            return CliParseHelpers.Error("--repeat-delay-ms must be >= 0", jsonOutput);
        }

        if (countdown < 0)
        {
            return CliParseHelpers.Error("--countdown must be >= 0", jsonOutput);
        }

        if (timeout < 0)
        {
            return CliParseHelpers.Error("--timeout must be >= 0", jsonOutput);
        }

        if (loop && !repeatProvided)
        {
            // --loop alone should represent infinite looping.
            repeat = 0;
        }

        if (repeatProvided && repeat > 0)
        {
            // Explicit finite repeat count implies loop semantics.
            loop = true;
        }

        if (repeatProvided && repeat == 0 && !loop)
        {
            return CliParseHelpers.Error("--repeat 0 requires --loop (infinite mode).", jsonOutput);
        }

        return CliParseResult.Success(new PlayCliOptions(
            macroFilePath,
            SpeedMultiplier: speed,
            Loop: loop,
            RepeatCount: repeat,
            RepeatDelayMs: repeatDelayMs,
            CountdownSeconds: countdown,
            TimeoutSeconds: timeout,
            DryRun: dryRun,
            JsonOutput: jsonOutput,
            LogLevel: logLevel));
    }
}
