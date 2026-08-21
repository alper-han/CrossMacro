namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// A validated PNG screenshot retained in memory with its capture metadata.
/// </summary>
public sealed record ScreenshotPngCaptureData(
    ReadOnlyMemory<byte> PngBytes,
    string? OutputPath,
    int Width,
    int Height,
    string Provider,
    bool IsRegion,
    bool CopiedToClipboard);
