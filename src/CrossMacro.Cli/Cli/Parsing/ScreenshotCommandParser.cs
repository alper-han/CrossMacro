using System;

namespace CrossMacro.Cli;

internal static class ScreenshotCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2 || CliParseHelpers.IsHelpToken(args[1]))
        {
            return args.Length >= 2 && CliParseHelpers.IsHelpToken(args[1])
                ? CliParseResult.Help("screenshot")
                : CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(args, 1,
                    "screenshot requires --output <path> or --clipboard.",
                    "crossmacro screenshot --output ./shot.png [--json] [--log-level <level>]",
                    "crossmacro screenshot --clipboard [--json] [--log-level <level>]",
                    "crossmacro screenshot --output ./crop.png --region <x> <y> <width> <height> [--json] [--log-level <level>]");
        }

        var jsonOutput = false;
        string? logLevel = null;
        string? outputPath = null;
        var clipboard = false;
        int? regionX = null, regionY = null, regionWidth = null, regionHeight = null;

        for (var i = 1; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "screenshot", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            if (string.Equals(args[i], "--output", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[i], "-o", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                    return CliParseHelpers.Error("--output requires a file path.", jsonOutput);
                outputPath = args[++i];
                continue;
            }

            if (string.Equals(args[i], "--clipboard", StringComparison.OrdinalIgnoreCase))
            {
                clipboard = true;
                continue;
            }

            if (string.Equals(args[i], "--region", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 4 >= args.Length)
                    return CliParseHelpers.Error("--region requires <x> <y> <width> <height>.", jsonOutput);

                if (!int.TryParse(args[i + 1], out var rx))
                    return CliParseHelpers.Error($"Invalid integer for region x: {args[i + 1]}", jsonOutput);
                if (!int.TryParse(args[i + 2], out var ry))
                    return CliParseHelpers.Error($"Invalid integer for region y: {args[i + 2]}", jsonOutput);
                if (!int.TryParse(args[i + 3], out var rw) || rw <= 0)
                    return CliParseHelpers.Error($"Invalid region width: {args[i + 3]}", jsonOutput);
                if (!int.TryParse(args[i + 4], out var rh) || rh <= 0)
                    return CliParseHelpers.Error($"Invalid region height: {args[i + 4]}", jsonOutput);

                try
                {
                    _ = checked(rx + rw);
                    _ = checked(ry + rh);
                }
                catch (OverflowException)
                {
                    return CliParseHelpers.Error("Region endpoint exceeds the supported screen coordinate range.", jsonOutput);
                }

                regionX = rx;
                regionY = ry;
                regionWidth = rw;
                regionHeight = rh;
                i += 4;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for screenshot: {args[i]}", jsonOutput);
        }

        if (string.IsNullOrWhiteSpace(outputPath) && !clipboard)
        {
            return CliParseHelpers.Error("screenshot requires --output <path> or --clipboard.", jsonOutput);
        }

        return CliParseResult.Success(new ScreenshotCliOptions(
            ScreenshotCliAction.Capture,
            outputPath,
            Clipboard: clipboard,
            RegionX: regionX,
            RegionY: regionY,
            RegionWidth: regionWidth,
            RegionHeight: regionHeight,
            JsonOutput: jsonOutput,
            LogLevel: logLevel));
    }
}
