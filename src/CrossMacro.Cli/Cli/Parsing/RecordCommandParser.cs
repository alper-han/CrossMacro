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

            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "record", ref jsonOutput, ref logLevel, i + 1, out var commonResult))
            {
                if (commonResult != null)
                {
                    return commonResult;
                }

                continue;
            }

            if (string.Equals(token, "--output", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "-o", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadNonEmptyString(args, ref i, out outputPath, out var outputError))
                {
                    return CliParseHelpers.Error(outputError, jsonOutput);
                }
                continue;
            }

            if (string.Equals(token, "--mouse", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadBool(args, ref i, out recordMouse, out var mouseError))
                {
                    return CliParseHelpers.Error(mouseError, jsonOutput);
                }
                continue;
            }

            if (string.Equals(token, "--keyboard", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadBool(args, ref i, out recordKeyboard, out var keyboardError))
                {
                    return CliParseHelpers.Error(keyboardError, jsonOutput);
                }
                continue;
            }

            if (string.Equals(token, "--mode", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadRecordMode(args, ref i, out mode, out var modeError))
                {
                    return CliParseHelpers.Error(modeError, jsonOutput);
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
                    return CliParseHelpers.Error(durationError, jsonOutput);
                }
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for record: {token}", jsonOutput);
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return CliParseHelpers.MissingRequiredOperands(
                "record requires --output <macro-file>.",
                jsonOutput,
                "crossmacro record (--output|-o) <macro-file> [--mouse <true|false>] [--keyboard <true|false>] [--mode <auto|absolute|relative>] [--skip-initial-zero] [--duration <sec>] [--json] [--log-level <level>]");
        }

        if (!recordMouse && !recordKeyboard)
        {
            return CliParseHelpers.Error("At least one of --mouse or --keyboard must be true.", jsonOutput);
        }

        if (durationSeconds < 0)
        {
            return CliParseHelpers.Error("--duration must be >= 0", jsonOutput);
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
