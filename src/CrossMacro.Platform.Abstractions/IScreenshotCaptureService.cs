
namespace CrossMacro.Platform.Abstractions;

public interface IScreenshotCaptureService
{
    /// <summary>
    /// Captures one validated PNG and keeps its encoded bytes in memory. The
    /// request may also ask the implementation to write those bytes to a file
    /// and/or copy them to the image clipboard. The encoded output is bounded;
    /// this contract does not promise a total peak-memory bound for the capture
    /// provider or PNG codec.
    /// </summary>
    public Task<ScreenshotPngCaptureResult> CapturePngAsync(
        ScreenshotPngCaptureRequest request,
        CancellationToken cancellationToken);

    public Task<ScreenshotCaptureResult> CaptureAsync(
        string? outputPath,
        bool copyToClipboard,
        ScreenRect? region,
        CancellationToken cancellationToken);
}
