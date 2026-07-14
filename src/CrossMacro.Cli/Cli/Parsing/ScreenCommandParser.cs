using System;
using System.Globalization;
using CrossMacro.Core.Models;
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
                : CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(args, 1, "screen requires pixel, wait-color, search-color, search-image, wait-image, or image-click.", "crossmacro screen pixel <x> <y> [--relative] [--timeout-ms <n>] [--json] [--log-level <level>]", "crossmacro screen wait-color <x> <y> <RRGGBB> [--timeout-ms <n>] [--json] [--log-level <level>]", "crossmacro screen search-color <x1> <y1> <x2> <y2> <RRGGBB> [--timeout-ms <n>] [--tolerance <0..255>] [--json] [--log-level <level>]", "crossmacro screen search-image <image-path> [--timeout-ms <n>] [--region <x> <y> <width> <height>] [--similarity <0..1>] [--downsample <n>] [--matchmode <first|best>] [--json] [--log-level <level>]", "crossmacro screen wait-image <image-path> [--timeout-ms <n>] [--region <x> <y> <width> <height>] [--similarity <0..1>] [--downsample <n>] [--matchmode <first|best>] [--json] [--log-level <level>]", "crossmacro screen image-click <image-path> [--timeout-ms <n>] [--button <left|right|middle>] [--region <x> <y> <width> <height>] [--similarity <0..1>] [--downsample <n>] [--matchmode <first|best>] [--json] [--log-level <level>]");
        }

        return args[1].ToLowerInvariant() switch
        {
            "pixel" => ParsePixel(args),
            "wait-color" => ParseWaitColor(args),
            "search-color" => ParseSearchColor(args),
            "search-image" => ParseSearchImage(args),
            "wait-image" => ParseImageAction(args, ScreenCliAction.WaitImage, "wait-image"),
            "image-click" => ParseImageAction(args, ScreenCliAction.ImageClick, "image-click"),
            _ => CliParseResult.Error($"Unknown screen subcommand: {args[1]}", prefersJsonOutput: CliParseHelpers.HasJsonOption(args, 2))
        };
    }

    private static CliParseResult ParsePixel(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        var relative = false;
        int? timeoutMs = null;
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

            if (string.Equals(args[i], "--timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out var parsedTimeout, out var timeoutError)) return CliParseHelpers.Error(timeoutError, jsonOutput);
                if (parsedTimeout < 0) return CliParseHelpers.Error("--timeout-ms must be >= 0", jsonOutput);
                timeoutMs = parsedTimeout;
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

        return CliParseResult.Success(new ScreenCliOptions(ScreenCliAction.Pixel, coordinates[0], coordinates[1], Relative: relative, TimeoutMs: timeoutMs, JsonOutput: jsonOutput, LogLevel: logLevel));
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
        int? timeoutMs = null;
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

            if (string.Equals(args[i], "--timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out var parsedTimeout, out var timeoutError)) return CliParseHelpers.Error(timeoutError, jsonOutput);
                if (parsedTimeout < 0) return CliParseHelpers.Error("--timeout-ms must be >= 0", jsonOutput);
                timeoutMs = parsedTimeout;
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
        if ((long)Math.Max(x1, x2) - Math.Min(x1, x2) > int.MaxValue
            || (long)Math.Max(y1, y2) - Math.Min(y1, y2) > int.MaxValue)
        {
            return CliParseHelpers.Error("screen search-color endpoint exceeds the supported screen coordinate range.", jsonOutput);
        }
        if (!ScreenPixelColor.TryParse(operands[4], out var color)) return CliParseHelpers.Error("Invalid color. Expected 6 hexadecimal RGB characters (RRGGBB).", jsonOutput);

        return CliParseResult.Success(new ScreenCliOptions(ScreenCliAction.SearchColor, x1, y1, color, X2: x2, Y2: y2, Tolerance: tolerance, TimeoutMs: timeoutMs, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseSearchImage(string[] args)
    {
        return ParseImageAction(args, ScreenCliAction.SearchImage, "search-image");
    }

    private static CliParseResult ParseImageAction(string[] args, ScreenCliAction action, string subcommand)
    {
        var jsonOutput = false;
        string? logLevel = null;
        string? imagePath = null;
        int? regionX = null;
        int? regionY = null;
        int? regionWidth = null;
        int? regionHeight = null;
        var similarity = 1.0;
        var downsample = 1;
        var matchMode = ScreenImageMatchMode.First;
        var scaleAware = false;
        int? timeoutMs = null;
        var button = MouseButton.Left;

        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, $"screen.{subcommand}", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            if (string.Equals(args[i], "--region", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadRegion(args, ref i, jsonOutput, out regionX, out regionY, out regionWidth, out regionHeight, out var regionError)) return regionError;
                continue;
            }

            if (string.Equals(args[i], "--similarity", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadDouble(args, ref i, out similarity, out var similarityError)) return CliParseHelpers.Error(similarityError, jsonOutput);
                if (!double.IsFinite(similarity) || similarity is < 0.0 or > 1.0) return CliParseHelpers.Error("--similarity must be a finite number between 0.0 and 1.0", jsonOutput);
                continue;
            }

            if (string.Equals(args[i], "--downsample", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out downsample, out var downsampleError)) return CliParseHelpers.Error(downsampleError, jsonOutput);
                if (downsample < 1) return CliParseHelpers.Error("--downsample must be >= 1", jsonOutput);
                continue;
            }

            if (string.Equals(args[i], "--matchmode", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length) return CliParseHelpers.Error("Missing value for --matchmode.", jsonOutput);
                matchMode = args[i].ToLowerInvariant() switch
                {
                    "first" => ScreenImageMatchMode.First,
                    "best" => ScreenImageMatchMode.Best,
                    _ => (ScreenImageMatchMode)(-1)
                };
                if (!Enum.IsDefined(matchMode)) return CliParseHelpers.Error("--matchmode must be first or best", jsonOutput);
                continue;
            }

            if (string.Equals(args[i], "--scale-aware", StringComparison.OrdinalIgnoreCase))
            {
                scaleAware = true;
                continue;
            }

            if (string.Equals(args[i], "--timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out var parsedTimeout, out var timeoutError)) return CliParseHelpers.Error(timeoutError, jsonOutput);
                if (parsedTimeout < 0) return CliParseHelpers.Error("--timeout-ms must be >= 0", jsonOutput);
                timeoutMs = parsedTimeout;
                continue;
            }

            if (string.Equals(args[i], "--button", StringComparison.OrdinalIgnoreCase))
            {
                if (action != ScreenCliAction.ImageClick) return CliParseHelpers.Error($"Unknown option for screen {subcommand}: {args[i]}", jsonOutput);
                if (++i >= args.Length) return CliParseHelpers.Error("Missing value for option.", jsonOutput);
                if (!TryParseMouseButton(args[i], out button)) return CliParseHelpers.Error("--button must be left, right, or middle", jsonOutput);
                continue;
            }

            if (args[i].StartsWith("-", StringComparison.Ordinal)) return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for screen {subcommand}: {args[i]}", jsonOutput);
            if (imagePath is not null) return CliParseHelpers.Error($"screen {subcommand} accepts exactly one <image-path> operand.", jsonOutput);
            imagePath = args[i];
        }

        if (imagePath is null) return CliParseHelpers.Error($"screen {subcommand} requires <image-path>.", jsonOutput);

        return CliParseResult.Success(new ScreenCliOptions(
            action,
            ImagePath: imagePath,
            RegionX: regionX,
            RegionY: regionY,
            RegionWidth: regionWidth,
            RegionHeight: regionHeight,
            Similarity: similarity,
            Downsample: downsample,
            MatchMode: matchMode,
            ScaleAware: scaleAware,
            TimeoutMs: timeoutMs,
            Button: button,
            JsonOutput: jsonOutput,
            LogLevel: logLevel));
    }

    private static bool TryParseMouseButton(string value, out MouseButton button)
    {
        button = value.ToLowerInvariant() switch
        {
            "left" => MouseButton.Left,
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.None
        };

        return button != MouseButton.None;
    }

    private static bool TryReadRegion(
        string[] args,
        ref int index,
        bool jsonOutput,
        out int? x,
        out int? y,
        out int? width,
        out int? height,
        out CliParseResult error)
    {
        x = y = width = height = null;
        error = default!;
        if (!CliParseHelpers.TryReadInt(args, ref index, out var parsedX, out var xError))
        {
            error = CliParseHelpers.Error(xError, jsonOutput);
            return false;
        }

        if (!CliParseHelpers.TryReadInt(args, ref index, out var parsedY, out var yError))
        {
            error = CliParseHelpers.Error(yError, jsonOutput);
            return false;
        }

        if (!CliParseHelpers.TryReadInt(args, ref index, out var parsedWidth, out var widthError))
        {
            error = CliParseHelpers.Error(widthError, jsonOutput);
            return false;
        }

        if (!CliParseHelpers.TryReadInt(args, ref index, out var parsedHeight, out var heightError))
        {
            error = CliParseHelpers.Error(heightError, jsonOutput);
            return false;
        }

        if (parsedWidth <= 0 || parsedHeight <= 0)
        {
            error = CliParseHelpers.Error("--region width and height must be positive", jsonOutput);
            return false;
        }

        try
        {
            _ = checked(parsedX + parsedWidth);
            _ = checked(parsedY + parsedHeight);
        }
        catch (OverflowException)
        {
            error = CliParseHelpers.Error("--region endpoint exceeds the supported screen coordinate range", jsonOutput);
            return false;
        }

        x = parsedX;
        y = parsedY;
        width = parsedWidth;
        height = parsedHeight;
        return true;
    }

    private static bool TryReadDouble(string[] args, ref int index, out double value, out string error)
    {
        if (++index >= args.Length)
        {
            value = 0;
            error = "Missing value for option.";
            return false;
        }

        if (!double.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            error = $"Invalid numeric value: {args[index]}";
            return false;
        }

        error = string.Empty;
        return true;
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
