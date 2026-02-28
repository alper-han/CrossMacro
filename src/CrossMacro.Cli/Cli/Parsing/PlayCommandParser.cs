using System;

namespace CrossMacro.Cli;

internal static class PlayCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2)
        {
            return CliParseResult.Error("Missing <macro-file> argument for play");
        }

        if (CliParseHelpers.IsHelpToken(args[1]))
        {
            return CliParseResult.Help("play");
        }

        var macroFilePath = args[1];
        if (string.IsNullOrWhiteSpace(macroFilePath))
        {
            return CliParseResult.Error("Macro file path cannot be empty");
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
                    return CliParseResult.Error(error);
                }
                continue;
            }

            if (string.Equals(token, "--repeat", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out repeat, out var error))
                {
                    return CliParseResult.Error(error);
                }
                repeatProvided = true;
                continue;
            }

            if (string.Equals(token, "--repeat-delay-ms", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out repeatDelayMs, out var error))
                {
                    return CliParseResult.Error(error);
                }
                continue;
            }

            if (string.Equals(token, "--countdown", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out countdown, out var error))
                {
                    return CliParseResult.Error(error);
                }
                continue;
            }

            if (string.Equals(token, "--timeout", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out timeout, out var error))
                {
                    return CliParseResult.Error(error);
                }
                continue;
            }

            if (CliParseHelpers.IsHelpToken(token))
            {
                return CliParseResult.Help("play");
            }

            return CliParseResult.Error($"Unknown option for play: {token}");
        }

        if (repeat < 0)
        {
            return CliParseResult.Error("--repeat must be >= 0");
        }

        if (repeatDelayMs < 0)
        {
            return CliParseResult.Error("--repeat-delay-ms must be >= 0");
        }

        if (countdown < 0)
        {
            return CliParseResult.Error("--countdown must be >= 0");
        }

        if (timeout < 0)
        {
            return CliParseResult.Error("--timeout must be >= 0");
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
            return CliParseResult.Error("--repeat 0 requires --loop (infinite mode).");
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
