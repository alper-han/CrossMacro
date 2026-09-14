namespace CrossMacro.Cli.Services.ScreenReading;

public sealed record ScreenshotData(
    string? OutputPath,
    int Width,
    int Height,
    string Format,
    string ProviderName,
    bool IsRegion,
    bool CopiedToClipboard);
