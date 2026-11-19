
namespace CrossMacro.Cli.Parsing;

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
                    CliUsage.ScreenshotLines);
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
                if (common is not null)
                {
                    return common;
                }

                continue;
            }

            if (string.Equals(args[i], "--output", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[i], "-o", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length || CliParseHelpers.LooksLikeOptionToken(args[i + 1]))
                {
                    return CliParseHelpers.Error("--output requires a file path.", jsonOutput);
                }

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
                var regionError = CliParseHelpers.ReadRegionOption(args, ref i, jsonOutput, out regionX, out regionY, out regionWidth, out regionHeight);
                if (regionError is not null)
                {
                    return regionError;
                }

                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for screenshot: {args[i]}", jsonOutput);
        }

        if (string.IsNullOrWhiteSpace(outputPath) && !clipboard)
        {
            return CliParseHelpers.Error("screenshot requires --output <path> or --clipboard.", jsonOutput);
        }

        return CliParseResult.Success(new ScreenshotOptions(
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
