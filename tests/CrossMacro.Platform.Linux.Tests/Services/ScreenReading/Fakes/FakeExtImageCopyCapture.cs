using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeExtImageCopyCapture : IExtImageCopyCapture
{
    private readonly ExtImageCopySupportResult _support;
    private readonly Queue<ExtImageCopyCaptureResult> _captureResults;

    public FakeExtImageCopyCapture(
        ExtImageCopySupportResult support,
        ExtImageCopyCaptureResult? captureResult = null)
    {
        _support = support;
        _captureResults = new Queue<ExtImageCopyCaptureResult>();
        _captureResults.Enqueue(captureResult ?? ExtImageCopyCaptureResult.Failure(
            ScreenReadErrorKind.CaptureFailed,
            "no fake ext-image-copy frame configured"));
    }

    public int CaptureCalls { get; private set; }

    public int ProbeCalls { get; private set; }

    public ScreenRect? LastRegion { get; private set; }

    public int DisposeCount { get; private set; }

    public Exception? CaptureException { get; init; }

    public void EnqueueCaptureResult(ExtImageCopyCaptureResult result) => _captureResults.Enqueue(result);

    public ExtImageCopySupportResult ProbeSupport()
    {
        ProbeCalls++;
        return _support;
    }

    public Task<ExtImageCopyCaptureResult> CaptureAsync(ScreenRect? region, ScreenReadOptions options)
    {
        return CaptureSupportedAsync(region, options);
    }

    public Task<ExtImageCopyCaptureResult> CaptureSupportedAsync(ScreenRect? region, ScreenReadOptions options)
    {
        CaptureCalls++;
        LastRegion = region;
        if (CaptureException is not null)
        {
            return Task.FromException<ExtImageCopyCaptureResult>(CaptureException);
        }

        var result = _captureResults.Count > 1 ? _captureResults.Dequeue() : _captureResults.Peek();
        return Task.FromResult(result);
    }

    public void Dispose() => DisposeCount++;
}
