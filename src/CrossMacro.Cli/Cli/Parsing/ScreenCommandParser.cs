using System;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Cli;

internal static class ScreenCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2 || CliParseHelpers.IsHelpToken(args[1]))
        {
            return args.Length >= 2 && CliParseHelpers.IsHelpToken(args[1])
                ? CliParseResult.Help("screen")
                : CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(args, 1, "screen requires pixel, wait-color, or search-color.", "crossmacro screen pixel <x> <y> [--json] [--log-level <level>]", "crossmacro screen pixel --relative <dx> <dy> [--json] [--log-level <level>]", "crossmacro screen wait-color <x> <y> <RRGGBB> [--timeout-ms <n>] [--json] [--log-level <level>]", "crossmacro screen search-color <x1> <y1> <x2> <y2> <RRGGBB> [--tolerance <0..255>] [--json] [--log-level <level>]");
        }

        return args[1].ToLowerInvariant() switch
        {
            "pixel" => ParsePixel(args),
            "wait-color" => ParseWaitColor(args),
            "search-color" => ParseSearchColor(args),
            _ => CliParseResult.Error($"Unknown screen subcommand: {args[1]}", prefersJsonOutput: CliParseHelpers.HasJsonOption(args, 2))
        };
    }

    private static CliParseResult ParsePixel(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        var relative = false;
        var coordinates = new System.Collections.Generic.List<int>();
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "screen.pixel", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            if (string.Equals(args[i], "--relative", StringComparison.OrdinalIgnoreCase))
            {
                relative = true;
                continue;
            }

            if (TryReadCoordinate(args[i], coordinates.Count == 0 ? "x" : "y", out var coordinate, out var error))
            {
                coordinates.Add(coordinate);
                continue;
            }

            return CliParseHelpers.Error(error, jsonOutput);
        }

        if (coordinates.Count != 2)
        {
            return CliParseHelpers.Error(relative ? "screen pixel --relative requires <dx> <dy>." : "screen pixel requires <x> <y>.", jsonOutput);
        }

        return CliParseResult.Success(new ScreenCliOptions(ScreenCliAction.Pixel, coordinates[0], coordinates[1], Relative: relative, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseWaitColor(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        int? timeoutMs = null;
        var operands = new System.Collections.Generic.List<string>();
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "screen.wait-color", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            if (string.Equals(args[i], "--timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out var parsedTimeout, out var timeoutError)) return CliParseHelpers.Error(timeoutError, jsonOutput);
                if (parsedTimeout < 0) return CliParseHelpers.Error("--timeout-ms must be >= 0", jsonOutput);
                timeoutMs = parsedTimeout;
                continue;
            }

            if (args[i].StartsWith("-", StringComparison.Ordinal) && !int.TryParse(args[i], out _)) return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for screen wait-color: {args[i]}", jsonOutput);
            operands.Add(args[i]);
        }

        if (operands.Count != 3) return CliParseHelpers.Error("screen wait-color requires <x> <y> <RRGGBB>.", jsonOutput);
        if (!TryParseInt(operands[0], "x", out var x, out var xError)) return CliParseHelpers.Error(xError, jsonOutput);
        if (!TryParseInt(operands[1], "y", out var y, out var yError)) return CliParseHelpers.Error(yError, jsonOutput);
        if (!ScreenPixelColor.TryParse(operands[2], out var color)) return CliParseHelpers.Error("Invalid color. Expected 6 hexadecimal RGB characters (RRGGBB).", jsonOutput);

        return CliParseResult.Success(new ScreenCliOptions(ScreenCliAction.WaitColor, x, y, color, TimeoutMs: timeoutMs, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseSearchColor(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        var tolerance = 0;
        var operands = new System.Collections.Generic.List<string>();
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "screen.search-color", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            if (string.Equals(args[i], "--tolerance", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out tolerance, out var toleranceError)) return CliParseHelpers.Error(toleranceError, jsonOutput);
                if (tolerance is < 0 or > byte.MaxValue) return CliParseHelpers.Error("--tolerance must be between 0 and 255", jsonOutput);
                continue;
            }

            if (args[i].StartsWith("-", StringComparison.Ordinal) && !int.TryParse(args[i], out _)) return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for screen search-color: {args[i]}", jsonOutput);
            operands.Add(args[i]);
        }

        if (operands.Count != 5) return CliParseHelpers.Error("screen search-color requires <x1> <y1> <x2> <y2> <RRGGBB>.", jsonOutput);
        if (!TryParseInt(operands[0], "x1", out var x1, out var x1Error)) return CliParseHelpers.Error(x1Error, jsonOutput);
        if (!TryParseInt(operands[1], "y1", out var y1, out var y1Error)) return CliParseHelpers.Error(y1Error, jsonOutput);
        if (!TryParseInt(operands[2], "x2", out var x2, out var x2Error)) return CliParseHelpers.Error(x2Error, jsonOutput);
        if (!TryParseInt(operands[3], "y2", out var y2, out var y2Error)) return CliParseHelpers.Error(y2Error, jsonOutput);
        if (x1 == x2 || y1 == y2) return CliParseHelpers.Error("screen search-color bounds must produce a positive end-exclusive region.", jsonOutput);
        if (!ScreenPixelColor.TryParse(operands[4], out var color)) return CliParseHelpers.Error("Invalid color. Expected 6 hexadecimal RGB characters (RRGGBB).", jsonOutput);

        return CliParseResult.Success(new ScreenCliOptions(ScreenCliAction.SearchColor, x1, y1, color, X2: x2, Y2: y2, Tolerance: tolerance, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static bool TryReadCoordinate(string token, string name, out int value, out string error)
    {
        if (token.StartsWith("-", StringComparison.Ordinal) && !int.TryParse(token, out _))
        {
            value = 0;
            error = $"Unknown option for screen pixel: {token}";
            return false;
        }

        return TryParseInt(token, name, out value, out error);
    }

    private static bool TryParseInt(string token, string name, out int value, out string error)
    {
        if (!int.TryParse(token, out value))
        {
            error = $"Invalid integer value for <{name}>: {token}";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
