namespace CrossMacro.Cli.Services.ScreenReading;

/// <summary>
/// The single mapping from screen read and screenshot failures to process exit codes,
/// shared by the CLI services and the MCP tools so both surfaces report the same code.
/// </summary>
public static class ScreenFailureExitCodes
{
    public static CliExitCode ForReadError(ScreenReadErrorKind? kind) => kind switch
    {
        ScreenReadErrorKind.InvalidArguments => CliExitCode.InvalidArguments,
        ScreenReadErrorKind.Unsupported or ScreenReadErrorKind.PermissionDenied or ScreenReadErrorKind.BackendUnavailable => CliExitCode.EnvironmentError,
        ScreenReadErrorKind.Canceled => CliExitCode.Cancelled,
        ScreenReadErrorKind.CaptureTimeout or ScreenReadErrorKind.OutOfBounds or ScreenReadErrorKind.CaptureFailed or ScreenReadErrorKind.ResourceLimitExceeded => CliExitCode.RuntimeError,
        null => CliExitCode.RuntimeError,
        _ => CliExitCode.RuntimeError,
    };

    public static CliExitCode ForScreenshot(ScreenshotCaptureFailureKind? failureKind, ScreenReadErrorKind? screenReadErrorKind) => failureKind switch
    {
        ScreenshotCaptureFailureKind.ProviderUnsupported or ScreenshotCaptureFailureKind.ClipboardUnsupported => CliExitCode.EnvironmentError,
        ScreenshotCaptureFailureKind.FileWriteFailed => CliExitCode.FileError,
        ScreenshotCaptureFailureKind.CaptureFailed => ForReadError(screenReadErrorKind),
        ScreenshotCaptureFailureKind.ClipboardWriteFailed => CliExitCode.RuntimeError,
        null => CliExitCode.RuntimeError,
        _ => CliExitCode.RuntimeError,
    };
}
