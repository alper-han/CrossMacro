namespace CrossMacro.Platform.Abstractions;

public enum ScreenshotCaptureFailureKind
{
    ProviderUnsupported,
    CaptureFailed,
    FileWriteFailed,
    ClipboardUnsupported,
    ClipboardWriteFailed,
}
