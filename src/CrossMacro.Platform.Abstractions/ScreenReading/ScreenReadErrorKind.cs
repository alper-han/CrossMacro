namespace CrossMacro.Platform.Abstractions.ScreenReading;

public enum ScreenReadErrorKind
{
    Unsupported,
    PermissionDenied,
    CaptureTimeout,
    OutOfBounds,
    BackendUnavailable,
    CaptureFailed,
    Canceled,
    ResourceLimitExceeded,
    InvalidArguments,
}
