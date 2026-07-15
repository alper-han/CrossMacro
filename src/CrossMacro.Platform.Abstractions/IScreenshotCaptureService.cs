
namespace CrossMacro.Platform.Abstractions;

public interface IScreenshotCaptureService
{
    public Task<ScreenshotCaptureResult> CaptureAsync(
        string? outputPath,
        bool copyToClipboard,
        ScreenRect? region,
        CancellationToken cancellationToken);
}
