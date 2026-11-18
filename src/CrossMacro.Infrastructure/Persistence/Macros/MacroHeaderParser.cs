using static CrossMacro.Infrastructure.Persistence.Macros.MacroFileLimits;

namespace CrossMacro.Infrastructure.Persistence.Macros;

/// <summary>
/// Applies the <c># Key: value</c> header comment lines of a macro file to a <see cref="MacroSequence"/>.
/// Unknown or malformed headers are ignored so older and newer files keep loading.
/// </summary>
internal static class MacroHeaderParser
{
    internal static void Apply(MacroSequence macro, string line)
    {
        if (line.StartsWith("# Name: ", StringComparison.Ordinal))
        {
            macro.Name = line.Substring(8).Trim();
        }
        else if (line.StartsWith(FormatHeader, StringComparison.Ordinal))
        {
            ValidateFormatVersion(line.AsSpan(FormatHeader.Length).Trim());
        }
        else if (line.StartsWith("# Created: ", StringComparison.Ordinal) && DateTime.TryParse(line.AsSpan(11).Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
        {
            macro.CreatedAt = date;
        }
        else if (line.StartsWith("# DurationMs: ", StringComparison.Ordinal) && long.TryParse(line.Substring(14).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var duration))
        {
            macro.TotalDurationMs = duration;
        }
        else if (line.StartsWith("# IsAbsolute: ", StringComparison.Ordinal) && bool.TryParse(line.Substring(14).Trim(), out var isAbsolute))
        {
            macro.IsAbsoluteCoordinates = isAbsolute;
        }
        else if (line.StartsWith("# SkipInitialZero: ", StringComparison.Ordinal) && bool.TryParse(line.Substring(19).Trim(), out var skipZero))
        {
            macro.SkipInitialZeroZero = skipZero;
        }
        else
        {
            ApplyTrailingDelay(macro, line);
        }
    }

    private static void ApplyTrailingDelay(MacroSequence macro, string line)
    {
        if (line.StartsWith(TrailingDelayMicrosecondsHeader, StringComparison.Ordinal))
        {
            if (long.TryParse(line.Substring(TrailingDelayMicrosecondsHeader.Length).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var trailingDelayMicroseconds)
                && trailingDelayMicroseconds >= 0)
            {
                macro.TrailingDelayMicroseconds = trailingDelayMicroseconds;
            }
        }
        else if (line.StartsWith(TrailingDelayHeader, StringComparison.Ordinal))
        {
            if (int.TryParse(line.Substring(TrailingDelayHeader.Length).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var trailingDelay))
            {
                macro.TrailingDelayMs = trailingDelay;
            }
        }
        else if (line.StartsWith(TrailingRandomDelayHeader, StringComparison.Ordinal))
        {
            var trailingRandomParts = line.Substring(TrailingRandomDelayHeader.Length).Trim().Split(',');
            if (trailingRandomParts.Length >= 2
                && int.TryParse(trailingRandomParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var trailingRandomMin)
                && int.TryParse(trailingRandomParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var trailingRandomMax))
            {
                macro.HasTrailingRandomDelay = true;
                macro.TrailingDelayMinMs = trailingRandomMin;
                macro.TrailingDelayMaxMs = trailingRandomMax;
            }
        }
        else if (line.StartsWith(TextInputBoundaryHeader, StringComparison.Ordinal))
        {
            ApplyTextInputBoundary(macro, line.Substring(TextInputBoundaryHeader.Length).Trim());
        }
        else if (line.StartsWith(ActionCommentHeader, StringComparison.Ordinal))
        {
            ApplyActionComment(macro, line.Substring(ActionCommentHeader.Length).Trim());
        }
    }

    private static void ApplyTextInputBoundary(MacroSequence macro, string encoded)
    {
        if (encoded.Length is 0)
        {
            return;
        }

        try
        {
            var boundaryBytes = Convert.FromBase64String(encoded);
            var boundaryJson = Encoding.UTF8.GetString(boundaryBytes);
            var boundary = JsonSerializer.Deserialize(boundaryJson, MacroFileJsonContext.Default.TextInputBoundary);
            if (boundary is { StartEventIndex: >= 0, EventCount: > 0 })
            {
                macro.TextInputBoundaries.Add(boundary);
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            Log.Warning(ex, "Ignoring malformed text input boundary metadata");
        }
    }

    private static void ApplyActionComment(MacroSequence macro, string encoded)
    {
        if (encoded.Length is 0)
        {
            return;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var boundary = JsonSerializer.Deserialize(json, MacroFileJsonContext.Default.ActionCommentBoundary);
            if (boundary is not null
                && boundary.EventStartIndex >= 0 && boundary.EventCount >= 0
                && boundary.ScriptStepStartIndex >= 0 && boundary.ScriptStepCount >= 0
                && boundary.Comment is { Length: > 0 and <= MaxActionCommentChars })
            {
                macro.ActionComments.Add(boundary);
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            Log.Warning(ex, "Ignoring malformed action comment metadata");
        }
    }

    private static void ValidateFormatVersion(ReadOnlySpan<char> format)
    {
        if (format.StartsWith(FormatVersionPrefix, StringComparison.Ordinal)
            && int.TryParse(format[FormatVersionPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            && version > CurrentFormatVersion)
        {
            throw new InvalidDataException($"Macro file format version {version.ToString(CultureInfo.InvariantCulture)} is newer than the supported version {CurrentFormatVersion.ToString(CultureInfo.InvariantCulture)}. Update CrossMacro to open this macro.");
        }
    }
}
