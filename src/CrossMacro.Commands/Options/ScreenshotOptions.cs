namespace CrossMacro.Commands.Options;

public sealed record ScreenshotOptions(
    string? OutputPath = null,
    bool Clipboard = false,
    int? RegionX = null,
    int? RegionY = null,
    int? RegionWidth = null,
    int? RegionHeight = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CommandOptions(JsonOutput, LogLevel);
