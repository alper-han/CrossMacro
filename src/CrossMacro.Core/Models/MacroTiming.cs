namespace CrossMacro.Core.Models;

/// <summary>Converts between the precise microsecond timeline and millisecond views.</summary>
public static class MacroTiming
{
    public const long MicrosecondsPerMillisecond = 1_000;

    public static int ToLegacyMilliseconds(long microseconds) =>
        checked((int)Math.Clamp(
            microseconds / MicrosecondsPerMillisecond,
            int.MinValue,
            int.MaxValue));

    public static long ToLegacyTimestampMilliseconds(long microseconds) =>
        microseconds / MicrosecondsPerMillisecond;

    /// <summary>Parses non-negative legacy millisecond or explicit ms/us durations.</summary>
    public static bool TryParseDurationMicroseconds(string? value, out long microseconds)
    {
        microseconds = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var token = value.Trim();
        var multiplier = MicrosecondsPerMillisecond;
        if (token.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            || token.EndsWith("us", StringComparison.OrdinalIgnoreCase)
            || token.EndsWith("µs", StringComparison.Ordinal))
        {
            multiplier = token.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
                ? MicrosecondsPerMillisecond
                : 1;
            token = token[..^2].TrimEnd();
        }

        if (!decimal.TryParse(
                token,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsed)
            || parsed < 0)
        {
            return false;
        }

        if (parsed > long.MaxValue / (decimal)multiplier)
        {
            return false;
        }

        var scaled = parsed * multiplier;
        if (scaled != decimal.Truncate(scaled) || scaled > long.MaxValue)
        {
            return false;
        }

        microseconds = decimal.ToInt64(scaled);
        return true;
    }

    /// <summary>Formats a non-negative duration using ms or us.</summary>
    public static string FormatDuration(long microseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(microseconds);

        if (microseconds % MicrosecondsPerMillisecond is 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{microseconds / MicrosecondsPerMillisecond}ms");
        }

        if (microseconds < MicrosecondsPerMillisecond)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{microseconds}us");
        }

        return (microseconds / (decimal)MicrosecondsPerMillisecond)
            .ToString("0.###", CultureInfo.InvariantCulture) + "ms";
    }

    /// <summary>Formats durations using the script's legacy unitless milliseconds when possible.</summary>
    public static string FormatScriptDuration(long microseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(microseconds);
        return microseconds % MicrosecondsPerMillisecond is 0
            ? (microseconds / MicrosecondsPerMillisecond).ToString(CultureInfo.InvariantCulture)
            : FormatDuration(microseconds);
    }
}
