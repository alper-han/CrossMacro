
namespace CrossMacro.Cli.Parsing;

internal static class CliParseHelpers
{
    public static CliParseResult MissingRequiredOperands(
        string message,
        bool jsonOutput,
        params string[] usageLines)
    {
        return CliParseResult.Error(
            message,
            UsageDetails(usageLines),
            prefersJsonOutput: jsonOutput);
    }

    public static CliParseResult MissingRequiredOperandsWithRemainingOptionsJson(
        string[] args,
        int startIndex,
        string message,
        params string[] usageLines)
    {
        return CliParseResult.Error(
            message,
            UsageDetails(usageLines),
            prefersJsonOutput: HasJsonOption(args, startIndex));
    }

    public static CliParseResult Error(string message, bool jsonOutput)
    {
        return CliParseResult.Error(message, prefersJsonOutput: jsonOutput);
    }

    public static CliParseResult Error(string message, IReadOnlyList<string> errorDetails, bool jsonOutput)
    {
        return CliParseResult.Error(message, errorDetails, prefersJsonOutput: jsonOutput);
    }

    public static CliParseResult ErrorWithRemainingOptionsJson(
        string[] args,
        int currentIndex,
        string message,
        bool jsonOutput)
    {
        return CliParseResult.Error(
            message,
            prefersJsonOutput: jsonOutput || HasJsonOption(args, currentIndex + 1));
    }

    public static CliParseResult ErrorWithRemainingOptionsJson(
        string[] args,
        int currentIndex,
        string message,
        IReadOnlyList<string> errorDetails,
        bool jsonOutput)
    {
        return CliParseResult.Error(
            message,
            errorDetails,
            prefersJsonOutput: jsonOutput || HasJsonOption(args, currentIndex + 1));
    }

    public static bool HasJsonOption(string[] args, int startIndex)
    {
        ArgumentNullException.ThrowIfNull(args);

        for (var i = Math.Max(startIndex, 0); i < args.Length; i++)
        {
            if (string.Equals(args[i], "--json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<string> UsageDetails(params string[] usageLines)
    {
        ArgumentNullException.ThrowIfNull(usageLines);

        if (usageLines.Length is 0)
        {
            return [];
        }

        var details = new string[usageLines.Length];
        for (var i = 0; i < usageLines.Length; i++)
        {
            details[i] = $"Usage: {usageLines[i]}";
        }

        return details;
    }

    public static bool TryHandleCommonCliOption(
        string[] args,
        ref int index,
        string helpTopic,
        ref bool jsonOutput,
        ref string? logLevel,
        out CliParseResult? result)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(helpTopic);

        var token = args[index];

        if (string.Equals(token, "--json", StringComparison.OrdinalIgnoreCase))
        {
            jsonOutput = true;
            result = null;
            return true;
        }

        if (string.Equals(token, "--log-level", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadLogLevel(args, ref index, out logLevel, out var logLevelError))
            {
                result = CliParseResult.Error(
                    logLevelError,
                    prefersJsonOutput: jsonOutput || HasJsonOption(args, index + 1));
                return true;
            }

            result = null;
            return true;
        }

        if (IsHelpToken(token))
        {
            result = CliParseResult.Help(helpTopic);
            return true;
        }

        result = null;
        return false;
    }

    public static bool TryReadInt(string[] args, ref int index, out int value, out string error)
    {
        if (index + 1 >= args.Length)
        {
            value = 0;
            error = $"Missing value after {args[index]}";
            return false;
        }

        index++;
        if (!int.TryParse(args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = $"Invalid integer value for {args[index - 1]}: {args[index]}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryReadDouble(string[] args, ref int index, out double value, out string error)
    {
        if (index + 1 >= args.Length)
        {
            value = 0;
            error = $"Missing value after {args[index]}";
            return false;
        }

        index++;
        if (!double.TryParse(args[index], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
        {
            error = $"Invalid numeric value for {args[index - 1]}: {args[index]}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryReadBool(string[] args, ref int index, out bool value, out string error)
    {
        if (index + 1 >= args.Length)
        {
            value = false;
            error = $"Missing value after {args[index]}";
            return false;
        }

        index++;
        if (!CliValueTokens.TryParseBool(args[index], out value))
        {
            error = $"Invalid boolean value for {args[index - 1]}: {args[index]}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryReadNonEmptyString(string[] args, ref int index, out string value, out string error)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            error = $"Missing value after {args[index]}";
            return false;
        }

        index++;
        value = args[index];
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"Value for {args[index - 1]} cannot be empty";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static CliParseResult? ReadStringOption(string[] args, ref int index, bool jsonOutput, out string? value)
    {
        if (TryReadNonEmptyString(args, ref index, out var parsed, out var error))
        {
            value = parsed;
            return null;
        }

        value = null;
        return Error(error, jsonOutput);
    }

    public static CliParseResult? ReadBoolOption(string[] args, ref int index, bool jsonOutput, out bool? value)
    {
        if (TryReadBool(args, ref index, out var parsed, out var error))
        {
            value = parsed;
            return null;
        }

        value = null;
        return Error(error, jsonOutput);
    }

    public static CliParseResult? ReadDoubleOption(string[] args, ref int index, bool jsonOutput, out double? value)
    {
        if (TryReadDouble(args, ref index, out var parsed, out var error))
        {
            value = parsed;
            return null;
        }

        value = null;
        return Error(error, jsonOutput);
    }

    public static CliParseResult? ReadRegionOption(
        string[] args,
        ref int index,
        bool jsonOutput,
        out int? x,
        out int? y,
        out int? width,
        out int? height)
    {
        x = y = width = height = null;
        if (!TryReadInt(args, ref index, out var parsedX, out var error)
            || !TryReadInt(args, ref index, out var parsedY, out error)
            || !TryReadInt(args, ref index, out var parsedWidth, out error)
            || !TryReadInt(args, ref index, out var parsedHeight, out error))
        {
            return Error(error, jsonOutput);
        }

        var rangeError = ScreenRegionRules.Validate(parsedX, parsedY, parsedWidth, parsedHeight, "--region");
        if (rangeError is not null)
        {
            return Error(rangeError, jsonOutput);
        }

        x = parsedX;
        y = parsedY;
        width = parsedWidth;
        height = parsedHeight;
        return null;
    }

    public static bool TryReadLogLevel(string[] args, ref int index, out string? logLevel, out string error)
    {
        if (index + 1 >= args.Length)
        {
            logLevel = null;
            error = $"Missing value after {args[index]}";
            return false;
        }

        index++;
        var token = args[index];
        if (!CliValueTokens.TryNormalizeLogLevel(token, out logLevel))
        {
            error = $"Invalid value for --log-level: {token}. Allowed: {string.Join(", ", CliValueTokens.LogLevels)}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool IsHelpToken(string token)
    {
        return string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsVersionToken(string token)
    {
        return string.Equals(token, "--version", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "-v", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeOptionToken(string token)
    {
        return !string.IsNullOrWhiteSpace(token)
            && token.StartsWith('-');
    }

    public static bool LooksLikeLongOptionToken(string token)
    {
        return !string.IsNullOrWhiteSpace(token)
            && (token.StartsWith("--", StringComparison.Ordinal)
                || IsHelpToken(token)
                || IsVersionToken(token));
    }
}
