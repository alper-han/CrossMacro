namespace CrossMacro.Platform.Abstractions;

public readonly record struct ScreenImageAutomationResult(
    bool IsSuccess,
    bool Found,
    ScreenPoint? Point,
    double? Score,
    ScreenReadErrorKind? ErrorKind,
    string? ErrorMessage)
{
    public static ScreenImageAutomationResult FoundAt(ScreenPoint point, double score) =>
        new(IsSuccess: true, Found: true, point, score, ErrorKind: null, ErrorMessage: null);

    public static ScreenImageAutomationResult NotFound(string message) =>
        new(IsSuccess: false, Found: false, Point: null, Score: null, ScreenReadErrorKind.CaptureTimeout, message);

    public static ScreenImageAutomationResult Failure(ScreenReadErrorKind errorKind, string message) =>
        new(IsSuccess: false, Found: false, Point: null, Score: null, errorKind, message);
}
