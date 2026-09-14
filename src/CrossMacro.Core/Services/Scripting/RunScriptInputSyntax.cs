namespace CrossMacro.Core.Services.Scripting;

/// <summary>Shared input-command grammar for compilation and editor projection. A recognized invalid command returns an error.</summary>
public static class RunScriptInputSyntax
{
    public static bool TryParseDelay(
        string step,
        out bool hasRandomDelay,
        out long fixedDelayMicroseconds,
        out int randomDelayMinMs,
        out int randomDelayMaxMs,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(step);
        hasRandomDelay = false;
        fixedDelayMicroseconds = 0;
        randomDelayMinMs = 0;
        randomDelayMaxMs = 0;
        error = null;
        if (!step.StartsWith("delay ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[6..].Trim();
        if (payload.StartsWith("random ", StringComparison.OrdinalIgnoreCase))
        {
            var randomPayload = payload[7..].Trim();
            var randomParts = randomPayload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            int minDelayMs;
            int maxDelayMs;

            if (randomParts.Length is 1 && randomParts[0].Contains("..", StringComparison.Ordinal))
            {
                var range = randomParts[0].Split("..", 2, StringSplitOptions.TrimEntries);
                if (range.Length is not 2
                    || !int.TryParse(range[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out minDelayMs)
                    || !int.TryParse(range[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out maxDelayMs))
                {
                    error = "Invalid random delay range. Expected: delay random <min> <max> or delay random <min>..<max>.";
                    return true;
                }
            }
            else if (randomParts.Length is not 2
|| !int.TryParse(randomParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out minDelayMs)
|| !int.TryParse(randomParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out maxDelayMs))
            {
                error = "Invalid random delay syntax. Expected: delay random <min> <max> or delay random <min>..<max>.";
                return true;
            }

            if (minDelayMs < 0 || maxDelayMs < 0 || minDelayMs > maxDelayMs)
            {
                error = "Invalid random delay bounds. Expected 0 <= min <= max.";
                return true;
            }

            hasRandomDelay = true;
            randomDelayMinMs = minDelayMs;
            randomDelayMaxMs = maxDelayMs;
            return true;
        }

        if (!MacroTiming.TryParseDurationMicroseconds(payload, out fixedDelayMicroseconds))
        {
            error = "Invalid delay value. Expected: delay <ms|us> with a non-negative duration.";
            return true;
        }

        return true;
    }

    public static bool TryParseMove(
        string step,
        out MouseCoordinateMode coordinateMode,
        out MouseCoordinateSpace coordinateSpace,
        out int x,
        out int y,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(step);
        coordinateMode = MouseCoordinateMode.Relative;
        coordinateSpace = MouseCoordinateSpace.LogicalDesktop;
        x = 0;
        y = 0;
        error = null;

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 4 || !string.Equals(parts[0], "move", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!RunScriptSyntax.TryParseMouseMoveMode(parts[1], out coordinateMode, out coordinateSpace))
        {
            error = "Invalid move mode. Expected: abs|absolute|rel|relative|rel-logical|rel-raw.";
            return true;
        }

        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
            || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
        {
            error = "Invalid move coordinates. Expected integers.";
            return true;
        }

        return true;
    }

    public static bool TryParseButton(string step, string command, out MacroMouseButton button, out bool useCurrentPosition, out string? error)
    {
        ArgumentNullException.ThrowIfNull(step);
        button = MacroMouseButton.None;
        useCurrentPosition = false;
        error = null;
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !string.Equals(parts[0], command, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts.Length is 2)
        {
            if (!TryResolveButton(parts[1], out button))
            {
                error = $"Unknown mouse button '{parts[1]}'.";
                return true;
            }

            return true;
        }

        if (parts.Length is 3 && RunScriptSyntax.IsCurrentPositionToken(parts[1]))
        {
            if (!TryResolveButton(parts[2], out button))
            {
                error = $"Unknown mouse button '{parts[2]}'.";
                return true;
            }

            useCurrentPosition = true;
            return true;
        }

        error = $"Invalid {command} syntax. Expected: {command} <button> or {command} {RunScriptSyntax.CurrentPositionToken} <button>.";
        return true;
    }

    public static bool TryParseKey(string step, out bool isKeyDown, out string keyToken, out string? error)
    {
        ArgumentNullException.ThrowIfNull(step);
        isKeyDown = false;
        keyToken = string.Empty;
        error = null;

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 3 || !string.Equals(parts[0], "key", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(parts[1], "down", StringComparison.OrdinalIgnoreCase))
        {
            isKeyDown = true;
        }
        else if (string.Equals(parts[1], "up", StringComparison.OrdinalIgnoreCase))
        {
            isKeyDown = false;
        }
        else
        {
            error = "Invalid key action. Expected: key down <key> | key up <key>.";
            return true;
        }

        keyToken = parts[2];
        return true;
    }

    public static bool TryParseTap(string step, out string combo)
    {
        ArgumentNullException.ThrowIfNull(step);
        combo = string.Empty;
        if (!step.StartsWith("tap ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        combo = step[4..].Trim();
        return true;
    }

    public static bool TryParseType(string step, out string text)
    {
        ArgumentNullException.ThrowIfNull(step);
        text = string.Empty;
        if (!step.StartsWith("type ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        text = step[5..];
        return true;
    }

    public static bool TryParseScroll(string step, out MacroMouseButton button, out int count, out string? error)
    {
        ArgumentNullException.ThrowIfNull(step);
        button = MacroMouseButton.None;
        count = 1;
        error = null;

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || parts.Length > 3 || !string.Equals(parts[0], "scroll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        button = parts[1].ToUpperInvariant() switch
        {
            "UP" => MacroMouseButton.ScrollUp,
            "DOWN" => MacroMouseButton.ScrollDown,
            "LEFT" => MacroMouseButton.ScrollLeft,
            "RIGHT" => MacroMouseButton.ScrollRight,
            _ => MacroMouseButton.None,
        };

        if (button is MacroMouseButton.None)
        {
            error = "Unknown scroll direction. Expected: up|down|left|right.";
            return true;
        }

        if (parts.Length is 3
&& (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count <= 0))
        {
            error = "Invalid scroll count. Expected integer > 0.";
            return true;
        }

        return true;
    }

    public static bool TryResolveButton(string token, out MacroMouseButton button)
    {
        ArgumentNullException.ThrowIfNull(token);
        button = token.ToUpperInvariant() switch
        {
            "LEFT" or "L" => MacroMouseButton.Left,
            "RIGHT" or "R" => MacroMouseButton.Right,
            "MIDDLE" or "M" => MacroMouseButton.Middle,
            "SIDE1" or "SIDE" or "BACK" => MacroMouseButton.Side1,
            "SIDE2" or "EXTRA" or "FORWARD" => MacroMouseButton.Side2,
            _ => MacroMouseButton.None,
        };

        return button is not MacroMouseButton.None;
    }

}
