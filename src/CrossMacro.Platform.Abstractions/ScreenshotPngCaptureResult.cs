namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// The result of one screenshot capture whose encoded PNG bytes are retained in memory.
/// </summary>
public sealed record ScreenshotPngCaptureResult(
    bool Success,
    ScreenshotCaptureFailureKind? FailureKind,
    ScreenReadErrorKind? ScreenReadErrorKind,
    string Message,
    IReadOnlyList<string> Details,
    ScreenshotPngCaptureData? Data)
{
    public static ScreenshotPngCaptureResult Ok(ScreenshotPngCaptureData data) =>
        new(Success: true, FailureKind: null, ScreenReadErrorKind: null, string.Empty, [], data);

    public static ScreenshotPngCaptureResult Fail(
        ScreenshotCaptureFailureKind failureKind,
        string message,
        IReadOnlyList<string> details,
        ScreenReadErrorKind? screenReadErrorKind = null) =>
        new(Success: false, failureKind, screenReadErrorKind, message, details, Data: null);
}
