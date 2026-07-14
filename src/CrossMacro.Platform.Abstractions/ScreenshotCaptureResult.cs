using System.Collections.Generic;

namespace CrossMacro.Platform.Abstractions;

public sealed record ScreenshotCaptureResult(
    bool Success,
    ScreenshotCaptureFailureKind? FailureKind,
    ScreenReadErrorKind? ScreenReadErrorKind,
    string Message,
    IReadOnlyList<string> Details,
    ScreenshotCaptureData? Data)
{
    public static ScreenshotCaptureResult Ok(ScreenshotCaptureData data) =>
        new(true, null, null, string.Empty, [], data);

    public static ScreenshotCaptureResult Fail(
        ScreenshotCaptureFailureKind failureKind,
        string message,
        IReadOnlyList<string> details,
        ScreenReadErrorKind? screenReadErrorKind = null) =>
        new(false, failureKind, screenReadErrorKind, message, details, null);
}
