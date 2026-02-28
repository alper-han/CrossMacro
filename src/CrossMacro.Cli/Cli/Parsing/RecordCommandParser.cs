using System;

namespace CrossMacro.Cli;

internal static class RecordCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        var outputPath = string.Empty;
        var recordMouse = true;
        var recordKeyboard = true;
        var mode = RecordCoordinateMode.Auto;
        var skipInitialZero = false;
        var durationSeconds = 0;
        var jsonOutput = false;
        string? logLevel = null;

        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];

            if (CliParseHelpers.IsHelpToken(token))
            {
                return CliParseResult.Help("record");
            }

            if (string.Equals(token, "--output", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "-o", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadNonEmptyString(args, ref i, out outputPath, out var outputError))
                {
                    return CliParseResult.Error(outputError);
                }
                continue;
            }

            if (string.Equals(token, "--mouse", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadBool(args, ref i, out recordMouse, out var mouseError))
                {
                    return CliParseResult.Error(mouseError);
                }
                continue;
            }

            if (string.Equals(token, "--keyboard", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadBool(args, ref i, out recordKeyboard, out var keyboardError))
                {
                    return CliParseResult.Error(keyboardError);
                }
                continue;
            }

            if (string.Equals(token, "--mode", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadRecordMode(args, ref i, out mode, out var modeError))
                {
                    return CliParseResult.Error(modeError);
                }
                continue;
            }

            if (string.Equals(token, "--skip-initial-zero", StringComparison.OrdinalIgnoreCase))
            {
                skipInitialZero = true;
                continue;
            }

            if (string.Equals(token, "--duration", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out durationSeconds, out var durationError))
                {
                    return CliParseResult.Error(durationError);
                }
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

            return CliParseResult.Error($"Unknown option for record: {token}");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return CliParseResult.Error("Usage: record --output <macro-file> [--mouse <true|false>] [--keyboard <true|false>] [--mode <auto|absolute|relative>] [--skip-initial-zero] [--duration <sec>] [--json] [--log-level <level>]");
        }

        if (!recordMouse && !recordKeyboard)
        {
            return CliParseResult.Error("At least one of --mouse or --keyboard must be true.");
        }

        if (durationSeconds < 0)
        {
            return CliParseResult.Error("--duration must be >= 0");
        }

        return CliParseResult.Success(new RecordCliOptions(
            OutputFilePath: outputPath,
            RecordMouse: recordMouse,
            RecordKeyboard: recordKeyboard,
            CoordinateMode: mode,
            SkipInitialZero: skipInitialZero,
            DurationSeconds: durationSeconds,
            JsonOutput: jsonOutput,
            LogLevel: logLevel));
    }
}
