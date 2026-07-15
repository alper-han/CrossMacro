namespace CrossMacro.Cli;

public sealed record class ScreenshotCliOptions(
    ScreenshotCliAction Action,
    string? OutputPath = null,
    bool Clipboard = false,
    int? RegionX = null,
    int? RegionY = null,
    int? RegionWidth = null,
    int? RegionHeight = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
