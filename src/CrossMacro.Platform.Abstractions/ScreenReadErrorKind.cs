namespace CrossMacro.Platform.Abstractions;

public enum ScreenReadErrorKind
{
    Unsupported,
    PermissionDenied,
    CaptureTimeout,
    OutOfBounds,
    BackendUnavailable,
    CaptureFailed,
    Canceled
}
