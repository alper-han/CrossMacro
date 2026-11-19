
namespace CrossMacro.Cli.Parsing;

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

            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "record", ref jsonOutput, ref logLevel, out var commonResult))
            {
                if (commonResult is not null)
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
                if (!TryReadMode(args, ref i, out mode, out var modeError))
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
                CliUsage.Record);
        }

        if (!recordMouse && !recordKeyboard)
        {
            return CliParseHelpers.Error("At least one of --mouse or --keyboard must be true.", jsonOutput);
        }

        if (durationSeconds < 0)
        {
            return CliParseHelpers.Error("--duration must be >= 0", jsonOutput);
        }

        return CliParseResult.Success(new RecordOptions(
            OutputFilePath: outputPath,
            RecordMouse: recordMouse,
            RecordKeyboard: recordKeyboard,
            CoordinateMode: mode,
            SkipInitialZero: skipInitialZero,
            DurationSeconds: durationSeconds,
            JsonOutput: jsonOutput,
            LogLevel: logLevel));
    }

    private static bool TryReadMode(string[] args, ref int index, out RecordCoordinateMode mode, out string error)
    {
        if (index + 1 >= args.Length)
        {
            mode = RecordCoordinateMode.Auto;
            error = $"Missing value after {args[index]}";
            return false;
        }

        index++;
        var token = args[index];
        if (!CommandValueTokens.TryParseRecordCoordinateMode(token, out mode))
        {
            error = $"Invalid value for --mode: {token}. Allowed: auto, absolute, relative.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
