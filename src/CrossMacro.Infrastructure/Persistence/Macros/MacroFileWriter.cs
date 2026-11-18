using static CrossMacro.Infrastructure.Persistence.Macros.MacroFileLimits;

namespace CrossMacro.Infrastructure.Persistence.Macros;

/// <summary>
/// Serializes a validated <see cref="MacroSequence"/> into the line-based text macro format.
/// </summary>
internal static class MacroFileWriter
{
    internal static async Task WriteAsync(
        TextWriter writer,
        MacroSequence macro,
        IReadOnlyList<KeyValuePair<string, string>> imageAssets)
    {
        await WriteHeaderAsync(writer, macro).ConfigureAwait(false);
        await WriteMetadataAsync(writer, macro).ConfigureAwait(false);

        foreach (var image in imageAssets)
        {
            await writer.WriteLineAsync($"{ImageHeader}{image.Key} = {image.Value}").ConfigureAwait(false);
        }

        await writer.WriteLineAsync($"{FormatHeader}{FormatVersionPrefix}{CurrentFormatVersion.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
        await WriteScriptSectionAsync(writer, macro).ConfigureAwait(false);
        await WriteEventsSectionAsync(writer, macro).ConfigureAwait(false);
        await writer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task WriteHeaderAsync(TextWriter writer, MacroSequence macro)
    {
        await writer.WriteLineAsync($"# Name: {macro.Name}").ConfigureAwait(false);
        await writer.WriteLineAsync($"# Created: {macro.CreatedAt:O}").ConfigureAwait(false);
        await writer.WriteLineAsync($"# DurationMs: {macro.TotalDurationMs.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
        await writer.WriteLineAsync($"# IsAbsolute: {macro.IsAbsoluteCoordinates}").ConfigureAwait(false);
        await writer.WriteLineAsync($"# SkipInitialZero: {macro.SkipInitialZeroZero}").ConfigureAwait(false);
        if (macro.TrailingDelayMicroseconds > 0)
        {
            await writer.WriteLineAsync($"{TrailingDelayMicrosecondsHeader}{macro.TrailingDelayMicroseconds.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
        }

        if (macro.HasTrailingRandomDelay)
        {
            await writer.WriteLineAsync($"{TrailingRandomDelayHeader}{macro.TrailingDelayMinMs.ToString(CultureInfo.InvariantCulture)},{macro.TrailingDelayMaxMs.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
        }
    }

    private static async Task WriteMetadataAsync(TextWriter writer, MacroSequence macro)
    {
        foreach (var boundary in macro.TextInputBoundaries)
        {
            if (boundary.EventCount <= 0 || boundary.StartEventIndex < 0)
            {
                continue;
            }

            var json = JsonSerializer.Serialize(boundary, MacroFileJsonContext.Default.TextInputBoundary);
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            await writer.WriteLineAsync($"{TextInputBoundaryHeader}{encoded}").ConfigureAwait(false);
        }

        foreach (var boundary in macro.ActionComments)
        {
            if (string.IsNullOrEmpty(boundary.Comment) || boundary.Comment.Length > MaxActionCommentChars)
            {
                continue;
            }

            var json = JsonSerializer.Serialize(boundary, MacroFileJsonContext.Default.ActionCommentBoundary);
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            await writer.WriteLineAsync($"{ActionCommentHeader}{encoded}").ConfigureAwait(false);
        }
    }

    private static async Task WriteScriptSectionAsync(TextWriter writer, MacroSequence macro)
    {
        await writer.WriteLineAsync(ScriptSectionHeader).ConfigureAwait(false);
        foreach (var scriptStep in macro.ScriptSteps.Where(step => !string.IsNullOrWhiteSpace(step)))
        {
            var normalized = scriptStep.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var lines = normalized.Split('\n');
            await writer.WriteLineAsync(lines[0]).ConfigureAwait(false);
            for (var index = 1; index < lines.Length; index++)
            {
                await writer.WriteLineAsync($"{ScriptContinuationPrefix}{lines[index]}").ConfigureAwait(false);
            }
        }
    }

    private static async Task WriteEventsSectionAsync(TextWriter writer, MacroSequence macro)
    {
        await writer.WriteLineAsync(EventsSectionHeader).ConfigureAwait(false);
        foreach (var ev in macro.Events)
        {
            if (ev.DelayMicroseconds > 0)
            {
                await writer.WriteLineAsync($"WU,{ev.DelayMicroseconds.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
            }

            if (ev.HasRandomDelay)
            {
                await writer.WriteLineAsync($"WR,{ev.RandomDelayMinMs.ToString(CultureInfo.InvariantCulture)},{ev.RandomDelayMaxMs.ToString(CultureInfo.InvariantCulture)}").ConfigureAwait(false);
            }

            var eventLine = BuildEventLine(ev);
            if (eventLine is not null)
            {
                await writer.WriteLineAsync(eventLine).ConfigureAwait(false);
            }
        }
    }

    private static string? BuildEventLine(MacroEvent ev)
    {
        return ev.Type switch
        {
            EventType.MouseMove => BuildMouseMoveLine(ev),
            EventType.ButtonPress => BuildMouseButtonLine("P", ev),
            EventType.ButtonRelease => BuildMouseButtonLine("R", ev),
            EventType.Click => BuildMouseButtonLine("C", ev),
            EventType.KeyPress => $"KP,{ev.KeyCode.ToString(CultureInfo.InvariantCulture)}",
            EventType.KeyRelease => $"KR,{ev.KeyCode.ToString(CultureInfo.InvariantCulture)}",
            EventType.None => null,
            _ => null,
        };
    }

    private static string BuildMouseButtonLine(string command, MacroEvent ev)
    {
        if (ev.UseCurrentPosition)
        {
            return $"{command},{ev.X.ToString(CultureInfo.InvariantCulture)},{ev.Y.ToString(CultureInfo.InvariantCulture)},{ev.Button},CurrentPosition";
        }

        if (MacroPositionSemantics.IsNonScrollMouseButtonEvent(ev) && ev.CoordinateMode is not null)
        {
            return $"{command},{ToCoordinateModeToken(ev)},{ev.X.ToString(CultureInfo.InvariantCulture)},{ev.Y.ToString(CultureInfo.InvariantCulture)},{ev.Button}";
        }

        return $"{command},{ev.X.ToString(CultureInfo.InvariantCulture)},{ev.Y.ToString(CultureInfo.InvariantCulture)},{ev.Button}";
    }

    private static string BuildMouseMoveLine(MacroEvent ev)
    {
        return ev.CoordinateMode is not null ? $"M,{ToCoordinateModeToken(ev)},{ev.X.ToString(CultureInfo.InvariantCulture)},{ev.Y.ToString(CultureInfo.InvariantCulture)}"
            : $"M,{ev.X.ToString(CultureInfo.InvariantCulture)},{ev.Y.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string ToCoordinateModeToken(MacroEvent ev)
    {
        return RunScriptSyntax.ToMouseMoveModeToken(
            ev.CoordinateMode ?? MouseCoordinateMode.Relative,
            ev.CoordinateSpace ?? MouseCoordinateSpace.RawDevice);
    }
}
