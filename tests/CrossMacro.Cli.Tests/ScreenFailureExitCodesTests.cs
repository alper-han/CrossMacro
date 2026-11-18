namespace CrossMacro.Cli.Tests;

public sealed class ScreenFailureExitCodesTests
{
    [Theory]
    [InlineData(ScreenReadErrorKind.InvalidArguments, CliExitCode.InvalidArguments)]
    [InlineData(ScreenReadErrorKind.Unsupported, CliExitCode.EnvironmentError)]
    [InlineData(ScreenReadErrorKind.PermissionDenied, CliExitCode.EnvironmentError)]
    [InlineData(ScreenReadErrorKind.BackendUnavailable, CliExitCode.EnvironmentError)]
    [InlineData(ScreenReadErrorKind.Canceled, CliExitCode.Cancelled)]
    [InlineData(ScreenReadErrorKind.CaptureTimeout, CliExitCode.RuntimeError)]
    [InlineData(ScreenReadErrorKind.OutOfBounds, CliExitCode.RuntimeError)]
    [InlineData(ScreenReadErrorKind.CaptureFailed, CliExitCode.RuntimeError)]
    [InlineData(ScreenReadErrorKind.ResourceLimitExceeded, CliExitCode.RuntimeError)]
    [InlineData(null, CliExitCode.RuntimeError)]
    public void ForReadError_MapsEveryErrorKind(ScreenReadErrorKind? kind, CliExitCode expected)
    {
        Assert.Equal(expected, ScreenFailureExitCodes.ForReadError(kind));
    }

    [Theory]
    [InlineData(ScreenshotCaptureFailureKind.ProviderUnsupported, null, CliExitCode.EnvironmentError)]
    [InlineData(ScreenshotCaptureFailureKind.ClipboardUnsupported, null, CliExitCode.EnvironmentError)]
    [InlineData(ScreenshotCaptureFailureKind.FileWriteFailed, null, CliExitCode.FileError)]
    [InlineData(ScreenshotCaptureFailureKind.ClipboardWriteFailed, null, CliExitCode.RuntimeError)]
    [InlineData(ScreenshotCaptureFailureKind.CaptureFailed, null, CliExitCode.RuntimeError)]
    [InlineData(ScreenshotCaptureFailureKind.CaptureFailed, ScreenReadErrorKind.InvalidArguments, CliExitCode.InvalidArguments)]
    [InlineData(ScreenshotCaptureFailureKind.CaptureFailed, ScreenReadErrorKind.PermissionDenied, CliExitCode.EnvironmentError)]
    [InlineData(ScreenshotCaptureFailureKind.CaptureFailed, ScreenReadErrorKind.Canceled, CliExitCode.Cancelled)]
    public void ForScreenshot_MapsFailureAndUnderlyingReadError(
        ScreenshotCaptureFailureKind failureKind,
        ScreenReadErrorKind? readError,
        CliExitCode expected)
    {
        Assert.Equal(expected, ScreenFailureExitCodes.ForScreenshot(failureKind, readError));
    }
}
