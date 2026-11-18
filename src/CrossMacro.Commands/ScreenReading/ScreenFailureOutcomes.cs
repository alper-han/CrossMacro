namespace CrossMacro.Commands.ScreenReading;

/// <summary>
/// The single mapping from screen read and screenshot failures to outcome codes,
/// shared by the command services and the MCP tools so both surfaces report the same code.
/// </summary>
public static class ScreenFailureOutcomes
{
    public static ExecutionOutcomeCode ForReadError(ScreenReadErrorKind? kind) => kind switch
    {
        ScreenReadErrorKind.InvalidArguments => ExecutionOutcomeCode.InvalidArguments,
        ScreenReadErrorKind.Unsupported or ScreenReadErrorKind.PermissionDenied or ScreenReadErrorKind.BackendUnavailable => ExecutionOutcomeCode.EnvironmentError,
        ScreenReadErrorKind.Canceled => ExecutionOutcomeCode.Cancelled,
        ScreenReadErrorKind.CaptureTimeout or ScreenReadErrorKind.OutOfBounds or ScreenReadErrorKind.CaptureFailed or ScreenReadErrorKind.ResourceLimitExceeded => ExecutionOutcomeCode.RuntimeError,
        null => ExecutionOutcomeCode.RuntimeError,
        _ => ExecutionOutcomeCode.RuntimeError,
    };

    public static ExecutionOutcomeCode ForScreenshot(ScreenshotCaptureFailureKind? failureKind, ScreenReadErrorKind? screenReadErrorKind) => failureKind switch
    {
        ScreenshotCaptureFailureKind.ProviderUnsupported or ScreenshotCaptureFailureKind.ClipboardUnsupported => ExecutionOutcomeCode.EnvironmentError,
        ScreenshotCaptureFailureKind.FileWriteFailed => ExecutionOutcomeCode.FileError,
        ScreenshotCaptureFailureKind.CaptureFailed => ForReadError(screenReadErrorKind),
        ScreenshotCaptureFailureKind.ClipboardWriteFailed => ExecutionOutcomeCode.RuntimeError,
        null => ExecutionOutcomeCode.RuntimeError,
        _ => ExecutionOutcomeCode.RuntimeError,
    };
}
