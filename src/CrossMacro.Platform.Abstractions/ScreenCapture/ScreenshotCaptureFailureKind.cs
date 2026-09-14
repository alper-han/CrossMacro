namespace CrossMacro.Platform.Abstractions.ScreenCapture;

public enum ScreenshotCaptureFailureKind
{
    ProviderUnsupported,
    CaptureFailed,
    FileWriteFailed,
    ClipboardUnsupported,
    ClipboardWriteFailed,
}
