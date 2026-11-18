
namespace CrossMacro.Cli.Parsing;

internal static class ScreenCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2 || CliParseHelpers.IsHelpToken(args[1]))
        {
            return args.Length >= 2 && CliParseHelpers.IsHelpToken(args[1])
                ? CliParseResult.Help("screen")
                : CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(args, 1, "screen requires pixel, wait-color, search-color, search-image, wait-image, or image-click.", CliUsage.ScreenLines);
        }

        return args[1].ToLowerInvariant() switch
        {
            "pixel" => ParsePixel(args),
            "wait-color" => ParseWaitColor(args),
            "search-color" => ParseSearchColor(args),
            "search-image" => ParseImageAction(args, ScreenAction.SearchImage, "search-image"),
            "wait-image" => ParseImageAction(args, ScreenAction.WaitImage, "wait-image"),
            "image-click" => ParseImageAction(args, ScreenAction.ImageClick, "image-click"),
            _ => CliParseResult.Error($"Unknown screen subcommand: {args[1]}", prefersJsonOutput: CliParseHelpers.HasJsonOption(args, 2)),
        };
    }

    private static CliParseResult ParsePixel(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        var relative = false;
        var coordinates = new List<int>();
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "screen.pixel", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null)
                {
                    return common;
                }

                continue;
            }

            if (string.Equals(args[i], "--relative", StringComparison.OrdinalIgnoreCase))
            {
                relative = true;
                continue;
            }

            if (TryReadCoordinate(args[i], coordinates.Count is 0 ? "x" : "y", out var coordinate, out var error))
            {
                coordinates.Add(coordinate);
                continue;
            }

            return CliParseHelpers.Error(error, jsonOutput);
        }

        if (coordinates.Count is not 2)
        {
            return CliParseHelpers.Error(relative ? "screen pixel --relative requires <dx> <dy>." : "screen pixel requires <x> <y>.", jsonOutput);
        }

        return CliParseResult.Success(new ScreenOptions(ScreenAction.Pixel, coordinates[0], coordinates[1], Relative: relative, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseWaitColor(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        int? timeoutMs = null;
        var operands = new List<string>();
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "screen.wait-color", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null)
                {
                    return common;
                }

                continue;
            }

            if (string.Equals(args[i], "--timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out var parsedTimeout, out var timeoutError))
                {
                    return CliParseHelpers.Error(timeoutError, jsonOutput);
                }

                if (parsedTimeout < 0)
                {
                    return CliParseHelpers.Error("--timeout-ms must be >= 0", jsonOutput);
                }

                timeoutMs = parsedTimeout;
                continue;
            }

            if (args[i].StartsWith('-') && !int.TryParse(args[i], CultureInfo.InvariantCulture, out _))
            {
                return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for screen wait-color: {args[i]}", jsonOutput);
            }

            operands.Add(args[i]);
        }

        if (operands.Count is not 3)
        {
            return CliParseHelpers.Error("screen wait-color requires <x> <y> <RRGGBB>.", jsonOutput);
        }

        if (!TryParseInt(operands[0], "x", out var x, out var xError))
        {
            return CliParseHelpers.Error(xError, jsonOutput);
        }

        if (!TryParseInt(operands[1], "y", out var y, out var yError))
        {
            return CliParseHelpers.Error(yError, jsonOutput);
        }

        if (!ScreenPixelColor.TryParse(operands[2], out var color))
        {
            return CliParseHelpers.Error("Invalid color. Expected 6 hexadecimal RGB characters (RRGGBB).", jsonOutput);
        }

        return CliParseResult.Success(new ScreenOptions(ScreenAction.WaitColor, x, y, color, TimeoutMs: timeoutMs, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseSearchColor(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        var tolerance = 0;
        int? timeoutMs = null;
        var operands = new List<string>();
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "screen.search-color", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null)
                {
                    return common;
                }

                continue;
            }

            if (string.Equals(args[i], "--tolerance", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out tolerance, out var toleranceError))
                {
                    return CliParseHelpers.Error(toleranceError, jsonOutput);
                }

                if (tolerance is < 0 or > byte.MaxValue)
                {
                    return CliParseHelpers.Error("--tolerance must be between 0 and 255", jsonOutput);
                }

                continue;
            }

            if (string.Equals(args[i], "--timeout-ms", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out var parsedTimeout, out var timeoutError))
                {
                    return CliParseHelpers.Error(timeoutError, jsonOutput);
                }

                if (parsedTimeout < 0)
                {
                    return CliParseHelpers.Error("--timeout-ms must be >= 0", jsonOutput);
                }

                timeoutMs = parsedTimeout;
                continue;
            }

            if (args[i].StartsWith('-') && !int.TryParse(args[i], CultureInfo.InvariantCulture, out _))
            {
                return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for screen search-color: {args[i]}", jsonOutput);
            }

            operands.Add(args[i]);
        }

        if (operands.Count is not 5)
        {
            return CliParseHelpers.Error("screen search-color requires <x1> <y1> <x2> <y2> <RRGGBB>.", jsonOutput);
        }

        if (!TryParseInt(operands[0], "x1", out var x1, out var x1Error))
        {
            return CliParseHelpers.Error(x1Error, jsonOutput);
        }

        if (!TryParseInt(operands[1], "y1", out var y1, out var y1Error))
        {
            return CliParseHelpers.Error(y1Error, jsonOutput);
        }

        if (!TryParseInt(operands[2], "x2", out var x2, out var x2Error))
        {
            return CliParseHelpers.Error(x2Error, jsonOutput);
        }

        if (!TryParseInt(operands[3], "y2", out var y2, out var y2Error))
        {
            return CliParseHelpers.Error(y2Error, jsonOutput);
        }

        var left = Math.Min(x1, x2);
        var top = Math.Min(y1, y2);
        var regionError = ScreenRegionRules.Validate(left, top, (long)Math.Max(x1, x2) - left, (long)Math.Max(y1, y2) - top, "screen search-color region");
        if (regionError is not null)
        {
            return CliParseHelpers.Error(regionError, jsonOutput);
        }

        if (!ScreenPixelColor.TryParse(operands[4], out var color))
        {
            return CliParseHelpers.Error("Invalid color. Expected 6 hexadecimal RGB characters (RRGGBB).", jsonOutput);
        }

        return CliParseResult.Success(new ScreenOptions(ScreenAction.SearchColor, x1, y1, color, X2: x2, Y2: y2, Tolerance: tolerance, TimeoutMs: timeoutMs, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseImageAction(string[] args, ScreenAction action, string subcommand)
    {
        var jsonOutput = false;
        string? logLevel = null;
        string? imagePath = null;
        int? regionX = null;
        int? regionY = null;
        int? regionWidth = null;
        int? regionHeight = null;
        var similarity = ScreenImageMatchDefaults.Similarity;
        var matchMode = ScreenImageMatchDefaults.Mode;
        int? timeoutMs = null;
        var button = MacroMouseButton.Left;
        var hasRegion = false;
        var hasSimilarity = false;
        var hasMatchMode = false;
        var hasTimeout = false;
        var hasButton = false;

        for (var i = 2; i < args.Length; i++)
        {
            var optionKind = ScreenReadOptionGrammar.GetCliOptionKind(args[i]);
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, $"screen.{subcommand}", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null)
                {
                    return common;
                }

                continue;
            }

            if (optionKind is ScreenReadOptionKind.Region)
            {
                if (hasRegion)
                {
                    return CliParseHelpers.Error("Duplicate --region option.", jsonOutput);
                }

                var regionError = CliParseHelpers.ReadRegionOption(args, ref i, jsonOutput, out regionX, out regionY, out regionWidth, out regionHeight);
                if (regionError is not null)
                {
                    return regionError;
                }

                hasRegion = true;
                continue;
            }

            if (optionKind is ScreenReadOptionKind.Similarity)
            {
                if (hasSimilarity)
                {
                    return CliParseHelpers.Error("Duplicate --similarity option.", jsonOutput);
                }

                if (!CliParseHelpers.TryReadDouble(args, ref i, out similarity, out var similarityError))
                {
                    return CliParseHelpers.Error(similarityError, jsonOutput);
                }

                if (!double.IsFinite(similarity) || similarity is < 0.0 or > 1.0)
                {
                    return CliParseHelpers.Error("--similarity must be a finite number between 0.0 and 1.0", jsonOutput);
                }

                hasSimilarity = true;
                continue;
            }

            if (optionKind is ScreenReadOptionKind.MatchMode)
            {
                if (hasMatchMode)
                {
                    return CliParseHelpers.Error("Duplicate --matchmode option.", jsonOutput);
                }

                if (++i >= args.Length)
                {
                    return CliParseHelpers.Error("Missing value for --matchmode.", jsonOutput);
                }

                if (!ScreenImageMatchModeCodec.TryParse(args[i], out matchMode))
                {
                    return CliParseHelpers.Error("--matchmode must be auto, first, or best", jsonOutput);
                }

                hasMatchMode = true;
                continue;
            }

            if (optionKind is ScreenReadOptionKind.Timeout)
            {
                if (action is ScreenAction.SearchImage)
                {
                    return CliParseHelpers.Error($"Unknown option for screen {subcommand}: {args[i]}", jsonOutput);
                }

                if (hasTimeout)
                {
                    return CliParseHelpers.Error("Duplicate --timeout-ms option.", jsonOutput);
                }

                if (!CliParseHelpers.TryReadInt(args, ref i, out var parsedTimeout, out var timeoutError))
                {
                    return CliParseHelpers.Error(timeoutError, jsonOutput);
                }

                if (parsedTimeout < 0)
                {
                    return CliParseHelpers.Error("--timeout-ms must be >= 0", jsonOutput);
                }

                timeoutMs = parsedTimeout;
                hasTimeout = true;
                continue;
            }

            if (optionKind is ScreenReadOptionKind.Button)
            {
                if (hasButton)
                {
                    return CliParseHelpers.Error("Duplicate --button option.", jsonOutput);
                }

                if (action is not ScreenAction.ImageClick)
                {
                    return CliParseHelpers.Error($"Unknown option for screen {subcommand}: {args[i]}", jsonOutput);
                }

                if (++i >= args.Length)
                {
                    return CliParseHelpers.Error("Missing value for option.", jsonOutput);
                }

                if (!TryParseMouseButton(args[i], out button))
                {
                    return CliParseHelpers.Error("--button must be left, right, or middle", jsonOutput);
                }

                hasButton = true;
                continue;
            }

            if (args[i].StartsWith('-'))
            {
                return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for screen {subcommand}: {args[i]}", jsonOutput);
            }

            if (imagePath is not null)
            {
                return CliParseHelpers.Error($"screen {subcommand} accepts exactly one <image-path> operand.", jsonOutput);
            }

            imagePath = args[i];
        }

        if (imagePath is null)
        {
            return CliParseHelpers.Error($"screen {subcommand} requires <image-path>.", jsonOutput);
        }

        return CliParseResult.Success(new ScreenOptions(
            action,
            ImagePath: imagePath,
            RegionX: regionX,
            RegionY: regionY,
            RegionWidth: regionWidth,
            RegionHeight: regionHeight,
            Similarity: similarity,
            MatchMode: matchMode,
            TimeoutMs: timeoutMs,
            Button: button,
            JsonOutput: jsonOutput,
            LogLevel: logLevel));
    }

    private static bool TryParseMouseButton(string value, out MacroMouseButton button)
    {
        button = value.ToLowerInvariant() switch
        {
            "left" => MacroMouseButton.Left,
            "right" => MacroMouseButton.Right,
            "middle" => MacroMouseButton.Middle,
            _ => MacroMouseButton.None,
        };

        return button is not MacroMouseButton.None;
    }

    private static bool TryReadCoordinate(string token, string name, out int value, out string error)
    {
        if (token.StartsWith('-') && !int.TryParse(token, CultureInfo.InvariantCulture, out _))
        {
            value = 0;
            error = $"Unknown option for screen pixel: {token}";
            return false;
        }

        return TryParseInt(token, name, out value, out error);
    }

    private static bool TryParseInt(string token, string name, out int value, out string error)
    {
        if (!int.TryParse(token, CultureInfo.InvariantCulture, out value))
        {
            error = $"Invalid integer value for <{name}>: {token}";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
