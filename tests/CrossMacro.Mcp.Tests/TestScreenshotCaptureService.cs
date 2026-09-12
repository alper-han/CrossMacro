namespace CrossMacro.Mcp.Tests;

internal sealed class TestScreenshotCaptureService : IScreenshotCaptureService
    {
        public ScreenshotPngCaptureResult? Result { get; init; }

        public int CallCount { get; private set; }

        public ScreenshotPngCaptureRequest? LastRequest { get; private set; }

        public Task<ScreenshotPngCaptureResult> CapturePngAsync(ScreenshotPngCaptureRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            return Task.FromResult(Result ?? throw new InvalidOperationException("Screenshot result was not configured."));
        }

        public Task<ScreenshotCaptureResult> CaptureAsync(
            string? outputPath,
            bool copyToClipboard,
            ScreenRect? region,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
