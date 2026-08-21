namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Requested destinations for one screenshot capture whose encoded PNG remains
/// available in memory after any requested writes complete. The maximum applies
/// to encoded PNG bytes, not to total capture or codec memory.
/// </summary>
public sealed record ScreenshotPngCaptureRequest(
    string? OutputPath = null,
    bool CopyToClipboard = false,
    ScreenRect? Region = null,
    int MaximumEncodedBytes = ScreenshotPngCaptureLimits.MaximumEncodedBytes)
{
    public const int DefaultMaximumEncodedBytes = ScreenshotPngCaptureLimits.MaximumEncodedBytes;
}
