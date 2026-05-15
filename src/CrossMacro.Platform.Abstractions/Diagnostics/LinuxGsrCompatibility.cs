using System;

namespace CrossMacro.Platform.Abstractions.Diagnostics;

public static class LinuxGsrCompatibility
{
    public const string InputDevicesPath = "/proc/bus/input/devices";
    public const string VirtualKeyboardName = "gsr-ui virtual keyboard";

    public static bool ContainsGsrVirtualKeyboard(string? inputDevicesContent)
    {
        if (string.IsNullOrWhiteSpace(inputDevicesContent))
        {
            return false;
        }

        var hasMatchingName = false;
        var hasKeyboardHandler = false;
        var hasEventHandler = false;

        foreach (var rawLine in inputDevicesContent.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                if (hasMatchingName && hasKeyboardHandler && hasEventHandler)
                {
                    return true;
                }

                hasMatchingName = false;
                hasKeyboardHandler = false;
                hasEventHandler = false;
                continue;
            }

            if (line.StartsWith("N:", StringComparison.Ordinal))
            {
                hasMatchingName = HasExactDeviceName(line);
                continue;
            }

            if (line.StartsWith("H:", StringComparison.Ordinal))
            {
                var handlers = GetHandlers(line);
                hasKeyboardHandler = HasHandler(handlers, "kbd");
                hasEventHandler = Array.Exists(handlers, handler => handler.StartsWith("event", StringComparison.Ordinal));
            }
        }

        return hasMatchingName && hasKeyboardHandler && hasEventHandler;
    }

    private static bool HasExactDeviceName(string line)
    {
        return line.Contains($"Name=\"{VirtualKeyboardName}\"", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetHandlers(string line)
    {
        var handlersIndex = line.IndexOf("Handlers=", StringComparison.Ordinal);
        if (handlersIndex < 0)
        {
            return [];
        }

        return line[(handlersIndex + "Handlers=".Length)..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool HasHandler(string[] handlers, string expected)
    {
        return Array.Exists(handlers, handler => string.Equals(handler, expected, StringComparison.Ordinal));
    }
}
