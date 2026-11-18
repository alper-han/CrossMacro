namespace CrossMacro.Cli;

/// <summary>
/// The one place that defines which text spellings the CLI, the settings command and the MCP
/// tools accept for shared option values, so the surfaces cannot drift apart.
/// </summary>
public static class CliValueTokens
{
    public static IReadOnlyList<string> LogLevels { get; } = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    public static IReadOnlyList<string> MotionModes { get; } = ["precision", "strict-speed", "strictspeed", "strict"];

    public static IReadOnlyList<string> RecordCoordinateModes { get; } = ["auto", "absolute", "abs", "relative", "rel"];

    /// <summary>Accepts <c>true/false</c> plus the <c>1/yes/on</c> and <c>0/no/off</c> spellings.</summary>
    public static bool TryParseBool(string? token, out bool value)
    {
        if (bool.TryParse(token, out value))
        {
            return true;
        }

        if (token is not null)
        {
            if (token.Equals("1", StringComparison.OrdinalIgnoreCase)
                || token.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || token.Equals("on", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (token.Equals("0", StringComparison.OrdinalIgnoreCase)
                || token.Equals("no", StringComparison.OrdinalIgnoreCase)
                || token.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }
        }

        value = false;
        return false;
    }

    /// <summary>Resolves a case-insensitive log level spelling to its canonical name.</summary>
    public static bool TryNormalizeLogLevel(string? token, [NotNullWhen(true)] out string? logLevel)
    {
        logLevel = LogLevels.FirstOrDefault(level => level.Equals(token, StringComparison.OrdinalIgnoreCase));
        return logLevel is not null;
    }

    public static bool TryParseMotionMode(string? token, out MotionPlaybackMode mode)
    {
        switch (token?.Trim().ToLowerInvariant())
        {
            case "precision":
                mode = MotionPlaybackMode.Precision;
                return true;
            case "strict-speed" or "strictspeed" or "strict":
                mode = MotionPlaybackMode.StrictSpeed;
                return true;
            default:
                mode = MotionPlaybackMode.Precision;
                return false;
        }
    }

    public static bool TryParseRecordCoordinateMode(string? token, out RecordCoordinateMode mode)
    {
        switch (token?.Trim().ToLowerInvariant())
        {
            case "auto":
                mode = RecordCoordinateMode.Auto;
                return true;
            case "absolute" or "abs":
                mode = RecordCoordinateMode.Absolute;
                return true;
            case "relative" or "rel":
                mode = RecordCoordinateMode.Relative;
                return true;
            default:
                mode = RecordCoordinateMode.Auto;
                return false;
        }
    }

    /// <summary>Parses an enum member name case-insensitively and rejects undefined numeric values.</summary>
    public static bool TryParseDefinedEnum<TEnum>(string? token, out TEnum value)
        where TEnum : struct, Enum =>
        Enum.TryParse(token, ignoreCase: true, out value) && Enum.IsDefined(value);
}
