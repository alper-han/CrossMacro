namespace CrossMacro.Infrastructure.Persistence.Macros;

/// <summary>
/// Parses the mouse and keyboard event lines of the <c>[Events]</c> section.
/// </summary>
internal static class MacroEventLineParser
{
    /// <summary>
    /// Parses one non-wait event line. Returns false for unknown or too short lines and throws
    /// <see cref="FormatException"/>, <see cref="OverflowException"/> or <see cref="ArgumentException"/>
    /// for malformed numeric fields and tokens.
    /// </summary>
    internal static bool TryParse(string[] parts, string type, out MacroEvent ev)
    {
        ev = default;

        if ((type is "M" or "MOVE") && parts.Length >= 3)
        {
            ParseMouseMove(parts, ref ev);
            return true;
        }

        if ((type is "P" or "PRESS" or "R" or "RELEASE" or "C" or "CLICK") && parts.Length >= 4)
        {
            ParseMouseButton(parts, type, ref ev);
            return true;
        }

        if ((type is "KP" or "KEYPRESS" or "KR" or "KEYRELEASE") && parts.Length >= 2)
        {
            ev.Type = type is "KP" or "KEYPRESS" ? EventType.KeyPress : EventType.KeyRelease;
            ev.KeyCode = int.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture);
            ev.Button = MacroMouseButton.None;
            return true;
        }

        return false;
    }

    private static void ParseMouseMove(string[] parts, ref MacroEvent ev)
    {
        var coordinateIndex = 1;
        if (!int.TryParse(parts[coordinateIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x))
        {
            if (parts.Length < 4 || !TryParseCoordinateMode(parts[coordinateIndex], out var mode, out var space))
            {
                throw new FormatException($"Invalid coordinate mode token '{parts[coordinateIndex]}'");
            }

            ev.CoordinateMode = mode;
            ev.CoordinateSpace = space;
            coordinateIndex++;
            x = int.Parse(parts[coordinateIndex], NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        ev.Type = EventType.MouseMove;
        ev.X = x;
        ev.Y = int.Parse(parts[coordinateIndex + 1], NumberStyles.Integer, CultureInfo.InvariantCulture);
        ev.Button = MacroMouseButton.None;
    }

    private static void ParseMouseButton(string[] parts, string type, ref MacroEvent ev)
    {
        var coordinateIndex = 1;
        MouseCoordinateMode? coordinateMode = null;
        MouseCoordinateSpace? coordinateSpace = null;
        if (!int.TryParse(parts[coordinateIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x))
        {
            if (parts.Length < 5 || !TryParseCoordinateMode(parts[coordinateIndex], out var mode, out var space))
            {
                throw new FormatException($"Invalid coordinate mode token '{parts[coordinateIndex]}'");
            }

            coordinateMode = mode;
            coordinateSpace = space;
            coordinateIndex++;
            x = int.Parse(parts[coordinateIndex], NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        ev.Type = type switch
        {
            "P" or "PRESS" => EventType.ButtonPress,
            "R" or "RELEASE" => EventType.ButtonRelease,
            _ => EventType.Click,
        };
        ev.X = x;
        ev.Y = int.Parse(parts[coordinateIndex + 1], NumberStyles.Integer, CultureInfo.InvariantCulture);
        ev.Button = Enum.Parse<MacroMouseButton>(parts[coordinateIndex + 2], ignoreCase: true);
        ev.UseCurrentPosition = parts.Length > coordinateIndex + 3 && IsCurrentPositionToken(parts[coordinateIndex + 3]);
        if (!ev.UseCurrentPosition && MacroPositionSemantics.IsNonScrollMouseButtonEvent(ev))
        {
            ev.CoordinateMode = coordinateMode;
            ev.CoordinateSpace = coordinateSpace;
        }
    }

    /// <summary>
    /// The writer emits only <c>CurrentPosition</c>. Older builds accepted the other spellings, so files that
    /// already exist on disk may carry them and must keep loading.
    /// </summary>
    private static bool IsCurrentPositionToken(string token)
    {
        var trimmed = token.Trim();
        return trimmed.Equals("CurrentPosition", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Current", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Live", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseCoordinateMode(
        string token,
        out MouseCoordinateMode mode,
        out MouseCoordinateSpace? space)
    {
        var normalized = token.Trim();
        if (normalized.Equals("rel", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("relative", StringComparison.OrdinalIgnoreCase))
        {
            mode = MouseCoordinateMode.Relative;
            space = null;
            return true;
        }

        if (RunScriptSyntax.TryParseMouseMoveMode(normalized, out mode, out var parsedSpace))
        {
            space = parsedSpace;
            return true;
        }

        space = null;
        return false;
    }
}
