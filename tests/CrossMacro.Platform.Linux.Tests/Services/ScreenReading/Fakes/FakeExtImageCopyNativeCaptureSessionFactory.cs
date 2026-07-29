
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeExtImageCopyNativeCaptureSessionFactory(ExtImageCopyCaptureResult result) : IExtImageCopyNativeCaptureSessionFactory, IDisposable
{
    private readonly ExtImageCopyCaptureResult _result = result;

    public int CaptureCalls { get; private set; }

    public ScreenRect? LastRegion { get; private set; }

    public int DisposeCount { get; private set; }

    public TimeSpan DelayBeforeResult { get; init; }

    public async Task<ExtImageCopyCaptureResult> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        CaptureCalls++;
        LastRegion = region;
        if (DelayBeforeResult > TimeSpan.Zero)
        {
            await Task.Delay(DelayBeforeResult, options.CancellationToken)
                .WaitAsync(options.Timeout ?? Timeout.InfiniteTimeSpan, options.CancellationToken)
                ;
        }

        return _result;
    }

    public void Dispose() => DisposeCount++;
}
