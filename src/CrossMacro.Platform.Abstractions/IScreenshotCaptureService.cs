
namespace CrossMacro.Platform.Abstractions;

public interface IScreenshotCaptureService
{
    Task<ScreenshotCaptureResult> CaptureAsync(
        string? outputPath,
        bool copyToClipboard,
        ScreenRect? region,
        CancellationToken cancellationToken);
}
