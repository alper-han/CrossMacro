namespace CrossMacro.Commands.Tests;

public sealed class ScreenFailureOutcomesTests
{
    [Theory]
    [InlineData(ScreenReadErrorKind.InvalidArguments, ExecutionOutcomeCode.InvalidArguments)]
    [InlineData(ScreenReadErrorKind.Unsupported, ExecutionOutcomeCode.EnvironmentError)]
    [InlineData(ScreenReadErrorKind.PermissionDenied, ExecutionOutcomeCode.EnvironmentError)]
    [InlineData(ScreenReadErrorKind.BackendUnavailable, ExecutionOutcomeCode.EnvironmentError)]
    [InlineData(ScreenReadErrorKind.Canceled, ExecutionOutcomeCode.Cancelled)]
    [InlineData(ScreenReadErrorKind.CaptureTimeout, ExecutionOutcomeCode.RuntimeError)]
    [InlineData(ScreenReadErrorKind.OutOfBounds, ExecutionOutcomeCode.RuntimeError)]
    [InlineData(ScreenReadErrorKind.CaptureFailed, ExecutionOutcomeCode.RuntimeError)]
    [InlineData(ScreenReadErrorKind.ResourceLimitExceeded, ExecutionOutcomeCode.RuntimeError)]
    [InlineData(null, ExecutionOutcomeCode.RuntimeError)]
    public void ForReadError_MapsEveryErrorKind(ScreenReadErrorKind? kind, ExecutionOutcomeCode expected)
    {
        Assert.Equal(expected, ScreenFailureOutcomes.ForReadError(kind));
    }

    [Theory]
    [InlineData(ScreenshotCaptureFailureKind.ProviderUnsupported, null, ExecutionOutcomeCode.EnvironmentError)]
    [InlineData(ScreenshotCaptureFailureKind.ClipboardUnsupported, null, ExecutionOutcomeCode.EnvironmentError)]
    [InlineData(ScreenshotCaptureFailureKind.FileWriteFailed, null, ExecutionOutcomeCode.FileError)]
    [InlineData(ScreenshotCaptureFailureKind.ClipboardWriteFailed, null, ExecutionOutcomeCode.RuntimeError)]
    [InlineData(ScreenshotCaptureFailureKind.CaptureFailed, null, ExecutionOutcomeCode.RuntimeError)]
    [InlineData(ScreenshotCaptureFailureKind.CaptureFailed, ScreenReadErrorKind.InvalidArguments, ExecutionOutcomeCode.InvalidArguments)]
    [InlineData(ScreenshotCaptureFailureKind.CaptureFailed, ScreenReadErrorKind.PermissionDenied, ExecutionOutcomeCode.EnvironmentError)]
    [InlineData(ScreenshotCaptureFailureKind.CaptureFailed, ScreenReadErrorKind.Canceled, ExecutionOutcomeCode.Cancelled)]
    public void ForScreenshot_MapsFailureAndUnderlyingReadError(
        ScreenshotCaptureFailureKind failureKind,
        ScreenReadErrorKind? readError,
        ExecutionOutcomeCode expected)
    {
        Assert.Equal(expected, ScreenFailureOutcomes.ForScreenshot(failureKind, readError));
    }
}
