using System;
using System.Globalization;

namespace CrossMacro.Cli;

internal static class CliParseHelpers
{
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
        var token = args[index];
        if (bool.TryParse(token, out value))
        {
            error = string.Empty;
            return true;
        }

        if (string.Equals(token, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "on", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            error = string.Empty;
            return true;
        }

        if (string.Equals(token, "0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "no", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "off", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            error = string.Empty;
            return true;
        }

        error = $"Invalid boolean value for {args[index - 1]}: {token}";
        return false;
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

    public static bool TryReadRecordMode(string[] args, ref int index, out RecordCoordinateMode mode, out string error)
    {
        if (index + 1 >= args.Length)
        {
            mode = RecordCoordinateMode.Auto;
            error = $"Missing value after {args[index]}";
            return false;
        }

        index++;
        var token = args[index];
        if (string.Equals(token, "auto", StringComparison.OrdinalIgnoreCase))
        {
            mode = RecordCoordinateMode.Auto;
            error = string.Empty;
            return true;
        }

        if (string.Equals(token, "absolute", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "abs", StringComparison.OrdinalIgnoreCase))
        {
            mode = RecordCoordinateMode.Absolute;
            error = string.Empty;
            return true;
        }

        if (string.Equals(token, "relative", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "rel", StringComparison.OrdinalIgnoreCase))
        {
            mode = RecordCoordinateMode.Relative;
            error = string.Empty;
            return true;
        }

        mode = RecordCoordinateMode.Auto;
        error = $"Invalid value for --mode: {token}. Allowed: auto, absolute, relative.";
        return false;
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
        var normalized = token.ToLowerInvariant() switch
        {
            "verbose" => "Verbose",
            "debug" => "Debug",
            "information" => "Information",
            "warning" => "Warning",
            "error" => "Error",
            "fatal" => "Fatal",
            _ => null
        };

        if (normalized == null)
        {
            logLevel = null;
            error = $"Invalid value for --log-level: {token}. Allowed: Verbose, Debug, Information, Warning, Error, Fatal.";
            return false;
        }

        logLevel = normalized;
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
}
