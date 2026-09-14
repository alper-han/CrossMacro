namespace CrossMacro.Platform.Abstractions.ScreenCapture;

public sealed record ScreenshotCaptureData(
    string? OutputPath,
    int Width,
    int Height,
    string Format,
    string Provider,
    bool IsRegion,
    bool CopiedToClipboard);
