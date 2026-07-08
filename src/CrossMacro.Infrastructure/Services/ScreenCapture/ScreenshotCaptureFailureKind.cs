namespace CrossMacro.Infrastructure.Services.ScreenCapture;

public enum ScreenshotCaptureFailureKind
{
    ProviderUnsupported,
    CaptureFailed,
    FileWriteFailed,
    ClipboardUnsupported,
    ClipboardWriteFailed
}
