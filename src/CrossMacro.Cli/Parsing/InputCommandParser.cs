using CrossMacro.Cli.Options;

namespace CrossMacro.Cli.Parsing;

internal static class InputCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length >= 2 && CliParseHelpers.IsHelpToken(args[1]))
        {
            return CliParseResult.Help(args[0]);
        }

        var operands = new List<string>();
        var jsonOutput = false;
        var dryRun = false;
        string? logLevel = null;

        for (var index = 1; index < args.Length; index++)
        {
            var token = args[index];
            if (CliParseHelpers.IsHelpToken(token))
            {
                return CliParseResult.Help(args[0]);
            }

            if (string.Equals(token, "--json", StringComparison.OrdinalIgnoreCase))
            {
                jsonOutput = true;
                continue;
            }

            if (string.Equals(token, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
                continue;
            }

            if (string.Equals(token, "--log-level", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadLogLevel(args, ref index, out logLevel, out var logLevelError))
                {
                    return CliParseHelpers.Error(logLevelError, jsonOutput || CliParseHelpers.HasJsonOption(args, index + 1));
                }

                continue;
            }

            if (token.StartsWith('-')
                && !int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return CliParseHelpers.ErrorWithRemainingOptionsJson(args, index, $"Unknown option for input command '{args[0]}': {token}", jsonOutput);
            }

            operands.Add(token);
        }

        if (operands.Count is 0)
        {
            return CliParseHelpers.MissingRequiredOperands(
                $"{args[0]} requires an input operation.",
                jsonOutput,
                "crossmacro move abs <x> <y> [--dry-run] [--json] [--log-level <level>]",
                "crossmacro click [current] <button> [--dry-run] [--json] [--log-level <level>]",
                "crossmacro key down|up <key> [--dry-run] [--json] [--log-level <level>]",
                "crossmacro tap <combo> [--dry-run] [--json] [--log-level <level>]",
                "crossmacro type <text> [--dry-run] [--json] [--log-level <level>]");
        }

        if (!TryBuildStep(args[0], operands, out var step, out var error))
        {
            return CliParseHelpers.Error(error, jsonOutput);
        }

        return CliParseResult.Success(new InputCliOptions(step, dryRun, jsonOutput, logLevel));
    }

    private static bool TryBuildStep(string command, IReadOnlyList<string> operands, out string step, out string error)
    {
        step = string.Empty;
        error = string.Empty;

        switch (command.ToLowerInvariant())
        {
            case "move":
                if (operands.Count is not 3 || !IsMoveMode(operands[0]))
                {
                    error = "Invalid move syntax. Expected: move abs|rel|rel-logical|rel-raw <x> <y>.";
                    return false;
                }

                step = $"move {string.Join(' ', operands)}";
                return true;

            case "click":
            case "down":
            case "up":
                if (operands.Count is 1)
                {
                    step = $"{command} {operands[0]}";
                    return true;
                }

                if (operands.Count is 2 && RunScriptSyntax.IsCurrentPositionToken(operands[0]))
                {
                    step = $"{command} {RunScriptSyntax.CurrentPositionToken} {operands[1]}";
                    return true;
                }

                error = $"Invalid {command} syntax. Expected: {command} <button> or {command} current <button>.";
                return false;

            case "scroll":
                if (operands.Count is < 1 or > 2
                    || !IsScrollDirection(operands[0])
                    || (operands.Count is 2 && (!int.TryParse(operands[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count <= 0)))
                {
                    error = "Invalid scroll syntax. Expected: scroll <up|down|left|right> [count], with count > 0.";
                    return false;
                }

                step = $"scroll {string.Join(' ', operands)}";
                return true;

            case "key":
                if (operands.Count is not 2
                    || (!string.Equals(operands[0], "down", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(operands[0], "up", StringComparison.OrdinalIgnoreCase)))
                {
                    error = "Invalid key syntax. Expected: key down <key> or key up <key>.";
                    return false;
                }

                step = $"key {string.Join(' ', operands)}";
                return true;

            case "tap":
                if (operands.Count is not 1 || string.IsNullOrWhiteSpace(operands[0]))
                {
                    error = "Invalid tap syntax. Expected: tap <combo>. Quote combinations that contain shell metacharacters.";
                    return false;
                }

                step = $"tap {operands[0]}";
                return true;

            case "type":
                if (operands.Count is not 1 || string.IsNullOrEmpty(operands[0]))
                {
                    error = "Invalid type syntax. Expected one quoted <text> operand.";
                    return false;
                }

                step = $"type {operands[0]}";
                return true;

            case "delay":
                if (operands.Count is 1 && int.TryParse(operands[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var delay) && delay >= 0)
                {
                    step = $"delay {delay.ToString(CultureInfo.InvariantCulture)}";
                    return true;
                }

                if (operands.Count is 3
                    && string.Equals(operands[0], "random", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(operands[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var min)
                    && int.TryParse(operands[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var max)
                    && min >= 0
                    && max >= min)
                {
                    step = $"delay random {min.ToString(CultureInfo.InvariantCulture)} {max.ToString(CultureInfo.InvariantCulture)}";
                    return true;
                }

                error = "Invalid delay syntax. Expected: delay <ms> or delay random <min> <max>.";
                return false;

            default:
                error = $"Unknown top-level input command: {command}.";
                return false;
        }
    }

    private static bool IsMoveMode(string value) =>
        string.Equals(value, "abs", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "absolute", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "rel", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "relative", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "rel-logical", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "relative-logical", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "rel-raw", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "relative-raw", StringComparison.OrdinalIgnoreCase);

    private static bool IsScrollDirection(string value) =>
        string.Equals(value, "up", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "down", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "left", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "right", StringComparison.OrdinalIgnoreCase);
}
