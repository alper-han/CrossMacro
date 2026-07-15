namespace CrossMacro.Cli.Services;

public sealed record class ScreenshotData(
    string? OutputPath,
    int Width,
    int Height,
    string Format,
    string ProviderName,
    bool IsRegion,
    bool CopiedToClipboard);
